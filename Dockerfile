ARG PROJ_NAME

FROM dotnetclub-docker.pkg.coding.net/dotnetconf/mcr/aspnet:2.2 AS base
WORKDIR /app
EXPOSE 80

FROM dotnetclub-docker.pkg.coding.net/dotnetconf/mcr/dotnet-core-sdk:2.2 AS build
ARG PROJ_NAME
COPY . /src/
WORKDIR /src/

RUN dotnet restore ${PROJ_NAME}.csproj
RUN dotnet build ${PROJ_NAME}.csproj -c Release -o /app

FROM build AS publish
ARG PROJ_NAME
RUN dotnet publish ${PROJ_NAME}.csproj -c Release -o /app

FROM base AS final
ARG PROJ_NAME
ENV APP_NAME=${PROJ_NAME}
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["/bin/bash", "-c", "dotnet ${APP_NAME}.dll"]


# docker build . -t dotnetclub-docker.pkg.coding.net/dotnetconf/dev/$(basename $(pwd) | cut -d '.' -f 2,3 | awk '{print tolower($0)}' | sed 's/\./\-/g'):$(date +"%Y%m%d-%H%M%S") --build-arg PROJ_NAME=$(basename $(pwd))
