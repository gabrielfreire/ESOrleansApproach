$_project = $Args[0]

if ($_project -eq $null) {
	write-output ""
	write-host "Project not provided" -fore red
	write-output ""
	write-host "-> run.ps1 <csproj project path>" -fore green
	write-host "    -->    i.e ./run.ps1 Grains.UnitTest\Grains.UnitTests.csproj" -fore green
	write-output ""
}
else {

	dotnet test $_project --collect:"XPlat Code Coverage"
}


# install dotnet tool running: dotnet tool install -g dotnet-reportgenerator-globaltool
# to generate report, get the GUID and use the command below replacing [GUID]
# -> reportgenerator -reports:"TestResults\[GUID]\coverage.cobertura.xml" -reporttypes:Html -targetdir:"TestResults"