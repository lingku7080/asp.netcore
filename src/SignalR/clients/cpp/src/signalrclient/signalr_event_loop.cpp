// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include "signalr_event_loop.h"

namespace signalr
{
    void signalr_event_loop::post(signalr_cb& cb)
    {
        {
            std::lock_guard<std::mutex> lock(m_lock);
            m_callbacks.push_back(std::shared_ptr<signalr_base_cb>(new signalr_cb(cb)));
        } // unlock
        m_cv.notify_one();
    }

    void signalr_event_loop::post(signalr_message_cb& cb)
    {
        {
            std::lock_guard<std::mutex> lock(m_lock);
            m_callbacks.push_back(std::shared_ptr<signalr_base_cb>(new signalr_message_cb(cb)));
        } // unlock
        m_cv.notify_one();
    }

    void signalr_event_loop::run()
    {
        m_event_loop_thread = std::thread([this]()
            {
                auto& callbacks = m_callbacks;
                auto& closed = m_closed;
                std::vector<std::shared_ptr<signalr_base_cb>> tmp;
                while (m_closed == false)
                {
                    {
                        std::unique_lock<std::mutex> lock(m_lock);
                        m_cv.wait(lock, [&callbacks, &closed] { return closed || !callbacks.empty(); });
                        tmp.swap(callbacks); // take all the callbacks while under the lock
                    } // unlock

                    for (auto& cb : tmp)
                    {
                        try
                        {
                            cb->invoke();
                        }
                        catch (...)
                        {
                            // ignore exceptions?
                        }
                    }

                    tmp.clear();
                }
            });
    }

    void signalr_event_loop::close()
    {
        m_closed = true;
        m_cv.notify_one();
        if (m_event_loop_thread.joinable())
        {
            m_event_loop_thread.join();
        }
    }

    signalr_event_loop::~signalr_event_loop()
    {
        close();
    }
}
