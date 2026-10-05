# Build context: repository root.   docker build -f src/SmartHadithTree.Api/Dockerfile -t hadith-api .
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/SmartHadithTree.Domain/*.csproj SmartHadithTree.Domain/
COPY src/SmartHadithTree.Application/*.csproj SmartHadithTree.Application/
COPY src/SmartHadithTree.Infrastructure/*.csproj SmartHadithTree.Infrastructure/
COPY src/SmartHadithTree.Api/*.csproj SmartHadithTree.Api/
RUN dotnet restore SmartHadithTree.Api/SmartHadithTree.Api.csproj
COPY src/SmartHadithTree.Domain/ SmartHadithTree.Domain/
COPY src/SmartHadithTree.Application/ SmartHadithTree.Application/
COPY src/SmartHadithTree.Infrastructure/ SmartHadithTree.Infrastructure/
COPY src/SmartHadithTree.Api/ SmartHadithTree.Api/
RUN dotnet publish SmartHadithTree.Api/SmartHadithTree.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    DisableHttpsRedirection=true
EXPOSE 8080
# Configure at run time (never bake secrets into the image):
#   ConnectionStrings__DefaultConnection, Gemini__ApiKey, Cors__AllowedOrigins__0
ENTRYPOINT ["dotnet", "SmartHadithTree.Api.dll"]
