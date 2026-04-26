FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for layer-cached restore
COPY src/Dodorassik.Core/Dodorassik.Core.csproj             src/Dodorassik.Core/
COPY src/Dodorassik.Infrastructure/Dodorassik.Infrastructure.csproj src/Dodorassik.Infrastructure/
COPY src/Dodorassik.Api/Dodorassik.Api.csproj               src/Dodorassik.Api/
RUN dotnet restore src/Dodorassik.Api/Dodorassik.Api.csproj

COPY . .
RUN dotnet publish src/Dodorassik.Api/Dodorassik.Api.csproj \
    -c Release -o /app/publish --no-restore

# ---- Runtime image ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Dodorassik.Api.dll"]
