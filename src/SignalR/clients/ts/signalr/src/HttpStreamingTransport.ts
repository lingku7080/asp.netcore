// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

import { HttpClient, HttpResponse } from "./HttpClient";
import { ILogger, LogLevel } from "./ILogger";
import { ITransport, TransferFormat } from "./ITransport";

// Not exported from 'index', this type is internal.
/** @private */
export class HttpStreamingTransport implements ITransport {
    private readonly httpClient: HttpClient;
    // @ts-ignore
    private readonly accessTokenFactory: (() => string | Promise<string>) | undefined;
    // @ts-ignore
    private readonly logger: ILogger;
    // @ts-ignore
    private readonly logMessageContent: boolean;
    private streamPromise: Promise<any>;
    private url: string = "";

    public onreceive: ((data: string | ArrayBuffer) => void) | null;
    public onclose: ((error?: Error) => void) | null;

    constructor(httpClient: HttpClient, accessTokenFactory: (() => string | Promise<string>) | undefined, logger: ILogger, logMessageContent: boolean) {
        if (!httpClient.supportsStreaming) {
            throw new Error("Streaming not supported in this environment.");
        }
        this.httpClient = httpClient;
        this.accessTokenFactory = accessTokenFactory;
        this.logger = logger;
        this.logMessageContent = logMessageContent;

        this.onreceive = null;
        this.onclose = null;
        this.streamPromise = Promise.resolve();
    }

    // @ts-ignore
    public async connect(url: string, transferFormat: TransferFormat): Promise<void> {
        this.url = url;
        const response = await this.httpClient.get(url, { stream: true });
        // @ts-ignore
        this.streamPromise = this.stream(response);
    }

    private async stream(response: HttpResponse): Promise<void> {
        if (response.content instanceof ReadableStream) {
            const reader = response.content!.getReader();
            let first = false;
            while (true) {
                // @ts-ignore
                const result = await reader.read();
                if (!first) {
                    first = true;
                    continue;
                }
                this.logger.log(LogLevel.Information, result.value);
                if (this.onreceive) {
                    this.onreceive(new TextDecoder("utf-8").decode((result.value as Uint8Array).buffer));
                }
                if (result.done) {
                    break;
                }
            }
        }
    }

    // @ts-ignore
    public async send(data: any): Promise<void> {
        await this.httpClient.post(this.url, { content: data });
    }

    public async stop(): Promise<void> {
        await this.streamPromise;
    }
}
