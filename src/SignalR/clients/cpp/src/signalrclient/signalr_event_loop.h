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
    typedef std::function<void()> signalr_base_cb;
    typedef std::function<void(std::exception_ptr)> signalr_cb;
    typedef std::function<void(std::string, std::exception_ptr)> signalr_message_cb;

    struct scheduler
    {
        virtual void schedule(signalr_cb& cb) = 0;
        virtual void schedule(signalr_cb& cb, std::exception_ptr) = 0;
        virtual void schedule(signalr_message_cb& cb, std::string) = 0;
        virtual void schedule(signalr_message_cb& cb, std::exception_ptr) = 0;
    };

    struct signalr_default_scheduler : scheduler
    {
        signalr_default_scheduler() : m_closed(false) {}
        signalr_default_scheduler(const signalr_default_scheduler&) = delete;
        signalr_default_scheduler& operator=(const signalr_default_scheduler&) = delete;

        void schedule(signalr_cb& cb);
        void schedule(signalr_cb& cb, std::exception_ptr);
        void schedule(signalr_message_cb& cb, std::string);
        void schedule(signalr_message_cb& cb, std::exception_ptr);
        ~signalr_default_scheduler();
        void run();

    private:
        std::vector<signalr_base_cb> m_callbacks;
        std::mutex m_lock;
        std::condition_variable m_cv;
        std::thread m_event_loop_thread;
        bool m_closed;

        void close();
        void schedule(signalr_base_cb cb);
    };
}
