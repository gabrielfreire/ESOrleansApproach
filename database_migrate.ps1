$_migrationName = $Args[0]
$_context = "ApplicationDbContext"

if ($_migrationName -eq $null) 
{
	write-host "Migration name not found" -fore red
}
else
{
	write-host "Migrating..." -fore blue
	write-host "Creating Migration $_migrationName" -fore blue
	dotnet ef migrations add $_migrationName -v --project src\Infrastructure\ --startup-project src\API --output-dir Persistence\Migrations\ --context $_context
	write-host "Migration $_migrationName created!" -fore blue
	write-host ""
}

