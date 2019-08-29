// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

import { HttpClient } from "./HttpClient";
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
    }

    // @ts-ignore
    public async connect(url: string, transferFormat: TransferFormat): Promise<void> {
        // @ts-ignore
        const response = await this.httpClient.get(url, { stream: true });
        if (response.content instanceof ReadableStream) {
            const reader = response.content.getReader();
            while (true) {
                // @ts-ignore
                const result = await reader.read();
                this.logger.log(LogLevel.Information, result.value);
            }
        }
        throw new Error("Method not implemented.");
    }

    // @ts-ignore
    public send(data: any): Promise<void> {
        throw new Error("Method not implemented.");
    }

    public stop(): Promise<void> {
        throw new Error("Method not implemented.");
    }
}
