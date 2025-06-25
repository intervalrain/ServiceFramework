FROM harbor.arfa.wise-paas.com/edge-coa/base-image:0.0.1 AS build

WORKDIR /app

# Copy the project files and restore dependencies.
COPY ["src/EdgeSync.ServiceFramework.Abstractions/EdgeSync.ServiceFramework.Abstractions.csproj", "/app/src/EdgeSync.ServiceFramework.Abstractions/EdgeSync.ServiceFramework.Abstractions.csproj"]
COPY ["src/EdgeSync.ServiceFramework.Core/EdgeSync.ServiceFramework.Core.csproj", "/app/src/EdgeSync.ServiceFramework.Core/EdgeSync.ServiceFramework.Core.csproj"]
COPY ["src/EdgeSync.ServiceFramework.DependencyInjection/EdgeSync.ServiceFramework.DependencyInjection.csproj", "/app/src/EdgeSync.ServiceFramework.DependencyInjection/EdgeSync.ServiceFramework.DependencyInjection.csproj"]
COPY ["src/EdgeSync.ServiceFramework.Testlib/EdgeSync.ServiceFramework.Testlib.csproj", "/app/src/EdgeSync.ServiceFramework.Testlib/EdgeSync.ServiceFramework.Testlib.csproj"]
COPY ["samples/BookStore/BookStore.Application/BookStore.Application.csproj", "/app/samples/BookStore/BookStore.Application/BookStore.Application.csproj"]
COPY ["samples/BookStore/BookStore.Domain/BookStore.Domain.csproj", "/app/samples/BookStore/BookStore.Domain/BookStore.Domain.csproj"]
COPY ["samples/BookStore/BookStore.Nats.Api/BookStore.Nats.Api.csproj", "/app/samples/BookStore/BookStore.Nats.Api/BookStore.Nats.Api.csproj"]
COPY ["samples/BookStore/BookStore.Nats.Client/BookStore.Nats.Client.csproj", "/app/samples/BookStore/BookStore.Nats.Client/BookStore.Nats.Client.csproj"]
COPY ["samples/BookStore/BookStore.Infrastructure/BookStore.Infrastructure.csproj", "/app/samples/BookStore/BookStore.Infrastructure/BookStore.Infrastructure.csproj"]
COPY ["samples/BookStore/BookStore.Web.Api/BookStore.Web.Api.csproj", "/app/samples/BookStore/BookStore.Web.Api/BookStore.Web.Api.csproj"]
COPY ["samples/BookStore/BookStore.sln", "/app/samples/BookStore/BookStore.sln"]

# COPY ["./README.md", "/app/README.md"]
COPY ./EdgeSync.sln /app/EdgeSync.sln
RUN dotnet restore "/app/EdgeSync.sln"

COPY . .

# Build and publish for ShadowAgent.Web
RUN dotnet publish "/app/EdgeSync.sln" -c Release -o /app/out --no-restore

RUN dotnet pack "/app/EdgeSync.sln" -c Release -o  /app/pkg


