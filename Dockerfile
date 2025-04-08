FROM harbor.arfa.wise-paas.com/edge-coa/base-image:0.0.1 AS build

WORKDIR /app

# Copy the project files and restore dependencies.
# COPY ["ShadowAgent.NatsApi/ShadowAgent.NatsApi.csproj", "/app/ShadowAgent.NatsApi/ShadowAgent.NatsApi.csproj"]


RUN dotnet restore "/app/ShadowAgent.NatsApi/ShadowAgent.NatsApi.csproj"

COPY . .

# Build and publish for ShadowAgent.Web
RUN dotnet publish "/app/ShadowAgent.NatsApi/ShadowAgent.NatsApi.csproj" -c Release -o /app/out --no-restore


# Stage 2: Run the Application
## below are the example commands to run the application.

# FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble AS runtime
# WORKDIR /app

# # Copy the published output from the build stage

# COPY --from=build /app/out_dbmigrator /dbmigrator
# COPY --from=build /app/out ./
# COPY --from=build /app/ShadowAgent/src/ShadowAgent.Web/appsettings.json /app/appsettings.json
# COPY --from=build /app/ShadowAgent/etc/dtdl /workspace/ShadowAgent/etc/dtdl
# COPY --from=build /app/ShadowAgent/etc/nats /app/nats
# COPY --from=build /app/openiddict.pfx ./

# # Expose necessary ports
# EXPOSE 44327

# # Set environment variables
# ENV ASPNETCORE_ENVIRONMENT=Production
# ENV ASPNETCORE_URLS=http://+:44327
# ENV NATS_CRED=/app/nats/shadowagent_dev.creds

# ENV MSG_BROKER_URL="nats://172.17.20.184:4222"
# ENV MSG_BUS_URL="nats://172.17.20.184:4222"
# ENV MSG_BROKER_CRED="/workspace/ShadowAgent/etc/nats/shadowagent_dev.creds"
# ENV MSG_BUS_CRED="/workspace/ShadowAgent/etc/nats/shadowagent_dev.creds"
# ENV ConnectionStrings__Default="Host=10.226.0.1;Port=5432;Database=ShadowAgent;Username=user;Password=password;Pooling=true;MinPoolSize=5;MaxPoolSize=50;Connection Idle Lifetime=10;Keepalive=10;Command Timeout=10;Timeout=10"

# # Run the application
# ENTRYPOINT ["dotnet", "ShadowAgent.Web.dll"]
