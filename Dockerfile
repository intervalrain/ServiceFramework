FROM harbor.arfa.wise-paas.com/edge-coa/base-image:0.0.1 AS build

WORKDIR /app

# Copy the project files and restore dependencies.
COPY ["src/EdgeSync.ServiceFramework.Abstractions/EdgeSync.ServiceFramework.Abstractions.csproj", "/app/src/EdgeSync.ServiceFramework.Abstractions/EdgeSync.ServiceFramework.Abstractions.csproj"]
COPY ["src/EdgeSync.ServiceFramework.Core/EdgeSync.ServiceFramework.Core.csproj", "/app/src/EdgeSync.ServiceFramework.Core/EdgeSync.ServiceFramework.Core.csproj"]
COPY ["src/EdgeSync.ServiceFramework.DependencyInjection/EdgeSync.ServiceFramework.DependencyInjection.csproj", "/app/src/EdgeSync.ServiceFramework.DependencyInjection/EdgeSync.ServiceFramework.DependencyInjection.csproj"]
COPY ["src/EdgeSync.ServiceFramework.Testlib/EdgeSync.ServiceFramework.Testlib.csproj", "/app/src/EdgeSync.ServiceFramework.Testlib/EdgeSync.ServiceFramework.Testlib.csproj"]
COPY ./EdgeSync.sln /app/EdgeSync.sln
RUN dotnet restore "/app/EdgeSync.sln"

COPY . .

# Build and publish for ShadowAgent.Web
RUN dotnet publish "/app/EdgeSync.sln" -c Release -o /app/out --no-restore

RUN dotnet pack "/app/EdgeSync.sln" -c Release -o  /app/pkg


