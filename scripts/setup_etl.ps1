$ErrorActionPreference = "Stop"

echo "Step 1: Create the console app"
dotnet new console -n SmartHadithTree.Etl -o ..\src\SmartHadithTree.Etl -f net9.0

echo "Step 2: Add it to the solution"
dotnet sln ..\SmartHadithTree.sln add ..\src\SmartHadithTree.Etl\SmartHadithTree.Etl.csproj

echo "Step 3: Add project references"
dotnet add ..\src\SmartHadithTree.Etl\SmartHadithTree.Etl.csproj reference ..\src\SmartHadithTree.Domain\SmartHadithTree.Domain.csproj ..\src\SmartHadithTree.Infrastructure\SmartHadithTree.Infrastructure.csproj

echo "Step 4: Add NuGet packages"
dotnet add ..\src\SmartHadithTree.Etl\SmartHadithTree.Etl.csproj package EFCore.BulkExtensions
dotnet add ..\src\SmartHadithTree.Etl\SmartHadithTree.Etl.csproj package Microsoft.Extensions.Hosting
dotnet add ..\src\SmartHadithTree.Etl\SmartHadithTree.Etl.csproj package Microsoft.EntityFrameworkCore.SqlServer -v "9.0.*"

echo "Step 5: Verify it builds"
dotnet build ..\SmartHadithTree.sln
