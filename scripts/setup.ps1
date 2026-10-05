$ErrorActionPreference = "Stop"
echo "Step 2: Create Domain"
dotnet new classlib -n SmartHadithTree.Domain -o ..\src\SmartHadithTree.Domain -f net9.0

echo "Step 3: Create Infrastructure"
dotnet new classlib -n SmartHadithTree.Infrastructure -o ..\src\SmartHadithTree.Infrastructure -f net9.0

echo "Step 4: Create Application"
dotnet new classlib -n SmartHadithTree.Application -o ..\src\SmartHadithTree.Application -f net9.0

echo "Step 5: Create Web API"
dotnet new webapi -n SmartHadithTree.Api -o ..\src\SmartHadithTree.Api -f net9.0 --no-openapi

echo "Step 6: Add projects to solution"
dotnet sln ..\SmartHadithTree.sln add ..\src\SmartHadithTree.Domain\SmartHadithTree.Domain.csproj ..\src\SmartHadithTree.Infrastructure\SmartHadithTree.Infrastructure.csproj ..\src\SmartHadithTree.Application\SmartHadithTree.Application.csproj ..\src\SmartHadithTree.Api\SmartHadithTree.Api.csproj

echo "Step 7: Add project references"
dotnet add ..\src\SmartHadithTree.Infrastructure\SmartHadithTree.Infrastructure.csproj reference ..\src\SmartHadithTree.Domain\SmartHadithTree.Domain.csproj
dotnet add ..\src\SmartHadithTree.Application\SmartHadithTree.Application.csproj reference ..\src\SmartHadithTree.Domain\SmartHadithTree.Domain.csproj
dotnet add ..\src\SmartHadithTree.Api\SmartHadithTree.Api.csproj reference ..\src\SmartHadithTree.Domain\SmartHadithTree.Domain.csproj ..\src\SmartHadithTree.Infrastructure\SmartHadithTree.Infrastructure.csproj ..\src\SmartHadithTree.Application\SmartHadithTree.Application.csproj

echo "Step 8: Add NuGet packages to Infrastructure"
dotnet add ..\src\SmartHadithTree.Infrastructure\SmartHadithTree.Infrastructure.csproj package Microsoft.EntityFrameworkCore.SqlServer
dotnet add ..\src\SmartHadithTree.Infrastructure\SmartHadithTree.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Tools

echo "Step 9: Add EF Design package to Api"
dotnet add ..\src\SmartHadithTree.Api\SmartHadithTree.Api.csproj package Microsoft.EntityFrameworkCore.Design

echo "Step 10: Delete default Class1.cs files"
Remove-Item ..\src\SmartHadithTree.Domain\Class1.cs
Remove-Item ..\src\SmartHadithTree.Infrastructure\Class1.cs
Remove-Item ..\src\SmartHadithTree.Application\Class1.cs

echo "Step 11: Verify solution builds"
dotnet build ..\SmartHadithTree.sln
