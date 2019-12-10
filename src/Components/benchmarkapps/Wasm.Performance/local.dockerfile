FROM selenium/standalone-chrome:3.141.59-mercury as final
COPY ./Driver/bin/Release/netcoreapp3.1/linux-x64/publish ./

ENTRYPOINT [ "./Wasm.Performance.Driver" ]