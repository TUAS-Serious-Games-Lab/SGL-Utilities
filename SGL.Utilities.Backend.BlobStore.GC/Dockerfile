#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:5.0-alpine AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine AS build
WORKDIR /src
COPY ["SGL.Utilities.Backend.BlobStore.GC/SGL.Utilities.Backend.BlobStore.GC.csproj", "SGL.Utilities.Backend.BlobStore.GC/"]
COPY ["SGL.Utilities.Backend.BlobStore/SGL.Utilities.Backend.BlobStore.csproj", "SGL.Utilities.Backend.BlobStore/"]
COPY ["SGL.Utilities/SGL.Utilities.csproj", "SGL.Utilities/"]
RUN dotnet restore "SGL.Utilities.Backend.BlobStore.GC/SGL.Utilities.Backend.BlobStore.GC.csproj"
COPY . .
WORKDIR "/src/SGL.Utilities.Backend.BlobStore.GC"
RUN dotnet build "SGL.Utilities.Backend.BlobStore.GC.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SGL.Utilities.Backend.BlobStore.GC.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
VOLUME /app/BlobStorage
ENTRYPOINT ["dotnet", "SGL.Utilities.Backend.BlobStore.GC.dll"]
