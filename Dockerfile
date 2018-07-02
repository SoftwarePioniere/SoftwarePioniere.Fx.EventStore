FROM microsoft/dotnet:2.1.300-sdk-alpine AS restore
ARG CONFIGURATION=Release
WORKDIR /proj
COPY nuget.config.build.tmp ./nuget.config
COPY Directory.Build.* ./
COPY *.sln ./
COPY src/SoftwarePioniere.DomainModel.Services.EventStore/*.csproj ./src/SoftwarePioniere.DomainModel.Services.EventStore/
COPY src/SoftwarePioniere.EventStore/*.csproj ./src/SoftwarePioniere.EventStore/
COPY src/SoftwarePioniere.EventStore.TestHarness/*.csproj ./src/SoftwarePioniere.EventStore.TestHarness/

COPY test/SoftwarePioniere.EventStore.Tests/*.csproj ./test/SoftwarePioniere.EventStore.Tests/
RUN dotnet restore SoftwarePioniere.Fx.EventStore.sln

FROM restore as src
COPY . .

FROM src AS buildsln
ARG CONFIGURATION=Release
ARG NUGETVERSIONV2=99.99.99
ARG ASSEMBLYSEMVER=99.99.99.99
WORKDIR /proj/src/
RUN dotnet build /proj/SoftwarePioniere.Fx.EventStore.sln -c $CONFIGURATION --no-restore /p:NuGetVersionV2=$NUGETVERSIONV2 /p:AssemblySemVer=$ASSEMBLYSEMVER

FROM buildsln as testrunner
ARG PROJECT=SoftwarePioniere.EventStore.Tests
# ARG CONFIGURATION=Release
# ARG NUGETVERSIONV2=99.99.99
# ARG ASSEMBLYSEMVER=99.99.99.99
WORKDIR /proj/test/$PROJECT
# ENTRYPOINT ["dotnet", "test", "--logger:trx"]
# RUN dotnet test -c $CONFIGURATION --no-restore --no-build /p:NuGetVersionV2=$NUGETVERSIONV2 /p:AssemblySemVer=$ASSEMBLYSEMVER -r /testresults

FROM buildsln as pack
ARG CONFIGURATION=Release
ARG NUGETVERSIONV2=99.99.99
ARG ASSEMBLYSEMVER=99.99.99.99
RUN dotnet pack /proj/SoftwarePioniere.Fx.EventStore.sln -c $CONFIGURATION --no-restore --no-build /p:NuGetVersionV2=$NUGETVERSIONV2 /p:AssemblySemVer=$ASSEMBLYSEMVER -o /proj/packages
WORKDIR /proj/packages/