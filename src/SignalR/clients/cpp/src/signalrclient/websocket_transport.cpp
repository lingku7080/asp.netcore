// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include "websocket_transport.h"
#include "logger.h"
#include "signalrclient/signalr_exception.h"

namespace signalr
{
    std::shared_ptr<transport> websocket_transport::create(const std::function<std::shared_ptr<websocket_client>()>& websocket_client_factory,
        const logger& logger, const std::function<void(const std::string &)>& process_response_callback,
        std::function<void(const std::exception&)> error_callback, scheduler& scheduler)
    {
        return std::shared_ptr<transport>(
            new websocket_transport(websocket_client_factory, logger, process_response_callback, error_callback, scheduler));
    }

    websocket_transport::websocket_transport(const std::function<std::shared_ptr<websocket_client>()>& websocket_client_factory,
        const logger& logger, const std::function<void(const std::string &)>& process_response_callback,
        std::function<void(const std::exception&)> error_callback, scheduler& scheduler)
        : transport(logger, process_response_callback, error_callback), m_websocket_client_factory(websocket_client_factory), m_scheduler(scheduler)
    {
        // we use this cts to check if the receive loop is running so it should be
        // initially cancelled to indicate that the receive loop is not running
        m_receive_loop_cts.cancel();
    }

    websocket_transport::~websocket_transport()
    {
        try
        {
            pplx::task_completion_event<void> event;
            disconnect([event](std::exception_ptr exception)
            {
                if (exception != nullptr)
                {
                    event.set_exception(exception);
                }
                else
                {
                    event.set();
                }
            });

            pplx::create_task(event).get();
        }
        catch (...) // must not throw from the destructor
        {}
    }

    transport_type websocket_transport::get_transport_type() const noexcept
    {
        return transport_type::websockets;
    }

    void websocket_transport::connect(const std::string& url, signalr_cb callback)
    {
        web::uri uri(utility::conversions::to_string_t(url));
        _ASSERTE(uri.scheme() == _XPLATSTR("ws") || uri.scheme() == _XPLATSTR("wss"));

        {
            std::lock_guard<std::mutex> stop_lock(m_start_stop_lock);

            if (!m_receive_loop_cts.get_token().is_canceled())
            {
                throw signalr_exception("transport already connected");
            }

            m_logger.log(trace_level::info,
                std::string("[websocket transport] connecting to: ")
                .append(url));

            auto websocket_client = m_websocket_client_factory();

            {
                std::lock_guard<std::mutex> client_lock(m_websocket_client_lock);
                m_websocket_client = websocket_client;
            }

            pplx::cancellation_token_source receive_loop_cts;

            auto transport = shared_from_this();

            websocket_client->connect(url)
                .then([transport, receive_loop_cts, callback](pplx::task<void> connect_task) mutable
                {
                    try
                    {
                        connect_task.get();
                        transport->receive_loop(receive_loop_cts);
                        callback(nullptr);
                    }
                    catch (const std::exception &e)
                    {
                        transport->m_logger.log(
                            trace_level::errors,
                            std::string("[websocket transport] exception when connecting to the server: ")
                            .append(e.what()));

                        receive_loop_cts.cancel();
                        callback(std::current_exception());
                    }
                });

            m_receive_loop_cts = receive_loop_cts;
        }
    }

    pplx::task<void> websocket_transport::send(const std::string &data)
    {
        // send will return a faulted task if client has disconnected
        return safe_get_websocket_client()->send(data);
    }

    void websocket_transport::disconnect(signalr_cb callback)
    {
        std::shared_ptr<websocket_client> websocket_client = nullptr;

        {
            std::lock_guard<std::mutex> lock(m_start_stop_lock);

            if (m_receive_loop_cts.get_token().is_canceled())
            {
                callback(nullptr);
            }

            m_receive_loop_cts.cancel();

            websocket_client = safe_get_websocket_client();
        }

        auto logger = m_logger;

        websocket_client->close()
            .then([logger, callback](pplx::task<void> close_task)
            mutable {
                try
                {
                    close_task.get();
                    callback(nullptr);
                }
                catch (const std::exception &e)
                {
                    logger.log(
                        trace_level::errors,
                        std::string("[websocket transport] exception when closing websocket: ")
                        .append(e.what()));

                    callback(std::make_exception_ptr(e));
                }
            });
    }

    // Note that the connection assumes that the error callback won't be fired when the result is being processed. This
    // may no longer be true when we replace the `receive_loop` with "on_message_received" and "on_close" events if they
    // can be fired on different threads in which case we will have to lock before setting groups token and message id.
    void websocket_transport::receive_loop(pplx::cancellation_token_source cts)
    {
        auto this_transport = shared_from_this();
        auto logger = this_transport->m_logger;

        // Passing the `std::weak_ptr<websocket_transport>` prevents from a memory leak where we would capture the shared_ptr to
        // the transport in the continuation lambda and as a result as long as the loop runs the ref count would never get to
        // zero. Now we capture the weak pointer and get the shared pointer only when the continuation runs so the ref count is
        // incremented when the shared pointer is acquired and then decremented when it goes out of scope of the continuation.
        auto weak_transport = std::weak_ptr<websocket_transport>(this_transport);

        auto websocket_client = this_transport->safe_get_websocket_client();

        // There are two cases when we exit the loop. The first case is implicit - we pass the cancellation_token
        // to `then` (note this is after the lambda body) and if the token is cancelled the continuation will not
        // run at all. The second - explicit - case happens if the token gets cancelled after the continuation has
        // been started in which case we just stop the loop by not scheduling another receive task.
        websocket_client->receive([weak_transport, cts, logger](std::string message, std::exception_ptr exception)
        {
            try
            {
                if (exception != nullptr)
                {
                    std::rethrow_exception(exception);
                }

                auto transport = weak_transport.lock();
                if (transport)
                {
                    transport->process_response(message);

                    if (!cts.get_token().is_canceled())
                    {
                        transport->receive_loop(cts);
                    }
                }
            }
            catch (const pplx::task_canceled&)
            {
                cts.cancel();

                logger.log(trace_level::info,
                    std::string("[websocket transport] receive task canceled."));
            }
            catch (const std::exception& e)
            {
                cts.cancel();

                logger.log(
                    trace_level::errors,
                    std::string("[websocket transport] error receiving response from websocket: ")
                    .append(e.what()));

                auto transport = weak_transport.lock();
                transport->safe_get_websocket_client()->close()
                    .then([](pplx::task<void> task)
                {
                    try { task.get(); }
                    catch (...) {}
                });

                if (transport)
                {
                    transport->error(e);
                }
            }
            catch (...)
            {
                cts.cancel();

                logger.log(
                    trace_level::errors,
                    std::string("[websocket transport] unknown error occurred when receiving response from websocket"));

                auto transport = weak_transport.lock();
                transport->safe_get_websocket_client()->close()
                    .then([](pplx::task<void> task)
                {
                    try { task.get(); }
                    catch (...) {}
                });

                if (transport)
                {
                    transport->error(signalr_exception("unknown error"));
                }
            }
        });
    }

    std::shared_ptr<websocket_client> websocket_transport::safe_get_websocket_client()
    {
        {
            const std::lock_guard<std::mutex> lock(m_websocket_client_lock);
            auto websocket_client = m_websocket_client;

            return websocket_client;
        }
    }
}
