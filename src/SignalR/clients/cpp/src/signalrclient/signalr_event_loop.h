// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma once

#include <condition_variable>
#include <exception>
#include <functional>
#include <mutex>
#include <vector>

namespace signalr
{
    struct signalr_cb;
    struct signalr_message_cb;
    struct signalr_base_cb;

    struct signalr_event_loop
    {
        void post(signalr_cb& cb);
        void post(signalr_message_cb& cb);
        void run();
        void close();
        ~signalr_event_loop();

    private:
        std::vector<std::shared_ptr<signalr_base_cb>> m_callbacks;
        std::mutex m_lock;
        std::condition_variable m_cv;
        std::thread m_event_loop_thread;
        bool m_closed;
    };

    struct signalr_base_cb
    {
    private:
        virtual void invoke() const = 0;

        friend signalr_event_loop;
    };

    struct signalr_cb : signalr_base_cb
    {
        signalr_cb(signalr_event_loop& event_loop, const std::function<void(std::exception_ptr)>& callback)
            : m_event_loop(event_loop), m_callback(callback) {}

        void operator()(const std::exception_ptr& e)
        {
            exception = e;
            m_event_loop.post(*this);
        }

    private:
        std::exception_ptr exception;
        std::function<void(std::exception_ptr)> m_callback;
        signalr_event_loop& m_event_loop;

        void invoke() const { m_callback(exception); }
    };

    struct signalr_message_cb : signalr_base_cb
    {
        signalr_message_cb(signalr_event_loop& event_loop,
            const std::function<void(std::string, std::exception_ptr)>& callback)
            : m_event_loop(event_loop), m_callback(callback) {}

        void operator()(std::string str, const std::exception_ptr& e)
        {
            s = str;
            exception = e;
            m_event_loop.post(*this);
        }

    private:
        std::string s;
        std::exception_ptr exception;
        std::function<void(std::string, std::exception_ptr)> m_callback;
        signalr_event_loop& m_event_loop;

        void invoke() const { m_callback(s, exception); }
    };
}
