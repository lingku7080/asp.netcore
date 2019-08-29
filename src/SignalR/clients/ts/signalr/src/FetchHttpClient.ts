// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

import { AbortError, HttpError, TimeoutError } from "./Errors";
import { HttpClient, HttpRequest, HttpResponse } from "./HttpClient";
import { ILogger, LogLevel } from "./ILogger";

export class FetchHttpClient extends HttpClient {
    private readonly logger: ILogger;

    public constructor(logger: ILogger) {
        super();
        this.logger = logger;
    }

    /** @inheritDoc */
    public async send(request: HttpRequest): Promise<HttpResponse> {
        // Check that abort was not signaled before calling send
        if (request.abortSignal && request.abortSignal.aborted) {
            return Promise.reject(new AbortError());
        }

        if (!request.method) {
            return Promise.reject(new Error("No method defined."));
        }
        if (!request.url) {
            return Promise.reject(new Error("No url defined."));
        }

        const abortController = new AbortController();

        const contentType = request.stream ? "application/bedrock-streaming;charset=UTF-8" : "text/plain;charset=UTF-8";

        const fetchRequest = new Request(request.url!, {
            body: request.content!,
            cache: "no-cache",
            credentials: "include",
            headers: {
                "Content-Type": contentType,
                "X-Requested-With": "XMLHttpRequest",
                ...request.headers,
            },
            method: request.method!,
            mode: "cors",
            redirect: "manual",
            signal: abortController.signal,
        });

        let error: any;
        // Hook our abortSignal into the abort controller
        if (request.abortSignal) {
            request.abortSignal.onabort = () => {
                abortController.abort();
                error = new AbortError();
            };
        }

        // If a timeout has been passed in, setup a timeout to call abort
        // Type needs to be any to fit window.setTimeout and NodeJS.setTimeout
        let timeoutId: any = null;
        if (request.timeout) {
            const msTimeout = request.timeout!;
            timeoutId = setTimeout(() => {
                abortController.abort();
                this.logger.log(LogLevel.Warning, `Timeout from HTTP request.`);
                error = new TimeoutError();
            }, msTimeout);
        }

        let response: Response;
        try {
            response = await fetch(fetchRequest);
        } catch (e) {
            if (error) {
                return Promise.reject(error);
            }
            this.logger.log(
                LogLevel.Warning,
                `Error from HTTP request. ${e}.`,
            );
            return Promise.reject(e);
        } finally {
            if (timeoutId) {
                clearTimeout(timeoutId);
            }
        }

        if (!response.ok) {
            return new HttpError(response.statusText, response.status);
        } else {
            if (request.abortSignal) {
                request.abortSignal.onabort = null;
            }

            try {
                if (request.stream && response.body) {
                    return new HttpResponse(response.status, response.statusText, response.body);
                }
                const content = deserializeContent(response, request.responseType);
                const payload = await content;

                return new HttpResponse(
                    response.status,
                    response.statusText,
                    payload,
                );
            } catch (e) {
                return Promise.reject(e);
            }
        }
    }
}

function deserializeContent(response: Response, responseType?: XMLHttpRequestResponseType): Promise<string | ArrayBuffer> {
    let content;
    switch (responseType) {
        case "arraybuffer":
            content = response.arrayBuffer();
            break;
        case "text":
            content = response.text();
            break;
        case "blob":
        case "document":
        case "json":
            throw new Error(`${responseType} is not supported.`);
        default:
            content = response.text();
            break;
    }

    return content;
}
