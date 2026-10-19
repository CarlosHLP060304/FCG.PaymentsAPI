FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["src/FCG.Payments.Api/FCG.Payments.Api.csproj", "src/FCG.Payments.Api/"]
COPY ["src/FCG.Payments.Application/FCG.Payments.Application.csproj", "src/FCG.Payments.Application/"]
COPY ["src/FCG.Payments.Domain/FCG.Payments.Domain.csproj", "src/FCG.Payments.Domain/"]
COPY ["src/FCG.Payments.Infrastructure/FCG.Payments.Infrastructure.csproj", "src/FCG.Payments.Infrastructure/"]
RUN dotnet restore "src/FCG.Payments.Api/FCG.Payments.Api.csproj"
COPY . .
RUN dotnet publish "src/FCG.Payments.Api/FCG.Payments.Api.csproj" --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM base AS final
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FCG.Payments.Api.dll"]
