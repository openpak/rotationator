# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY Rotationator/Rotationator.csproj Rotationator/
RUN dotnet restore Rotationator/Rotationator.csproj
COPY Rotationator Rotationator
RUN dotnet publish Rotationator/Rotationator.csproj -c Release -o /out /p:UseAppHost=false

FROM node:20-alpine AS deps
WORKDIR /uploader
COPY uploader/package.json uploader/package-lock.json ./
RUN npm ci --omit=dev

FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS final
RUN apk add --no-cache nodejs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
WORKDIR /app
COPY --from=build /out /app/rotationator
COPY --from=deps /uploader/node_modules /app/uploader/node_modules
COPY uploader/rotate.mjs uploader/package.json /app/uploader/
RUN mkdir /data && chown 1000:1000 /data
VOLUME /data
USER 1000:1000
CMD ["node", "/app/uploader/rotate.mjs"]
