# Export the current system's chains (Transmissions) for the given books to UTF-8 JSON.
# -Books holds substrings of Hadiths.BookName (default: al-Bukhari and al-Mustadrak).
param(
    [string]$Out = "$PSScriptRoot\current_chains.json",
    [string[]]$Books = @('البخاري', 'المستدرك')
)

$conn = New-Object System.Data.SqlClient.SqlConnection "Server=.;Database=SmartHadithTree;Integrated Security=True;TrustServerCertificate=True"
$conn.Open()

# "h.BookName LIKE @b0 OR h.BookName LIKE @b1 ..." with the values passed as parameters.
$bookFilter = ($Books | ForEach-Object -Begin { $i = 0 } -Process { "h.BookName LIKE @b$i"; $i++ }) -join ' OR '

function Query([string]$sql) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.CommandTimeout = 600
    for ($i = 0; $i -lt $Books.Count; $i++) {
        [void]$cmd.Parameters.AddWithValue("@b$i", "%$($Books[$i])%")
    }
    $table = New-Object System.Data.DataTable
    $table.Load($cmd.ExecuteReader())
    return ,$table
}

$hadiths = Query @"
SELECT h.Id, h.BookName, h.HadithNumber, LEFT(h.MatnArabic, 400) AS Head
FROM Hadiths h
WHERE $bookFilter
"@

$links = Query @"
SELECT t.HadithId, t.StepOrder, t.TransmissionTerm,
       st.FullName AS Student, st.ItqanId AS StudentItqan,
       sh.FullName AS Sheikh,  sh.ItqanId AS SheikhItqan, sh.ItqanGrade AS SheikhGrade
FROM Transmissions t
JOIN Hadiths h ON h.Id = t.HadithId
JOIN Narrators st ON st.Id = t.StudentId
JOIN Narrators sh ON sh.Id = t.SheikhId
WHERE $bookFilter
ORDER BY t.HadithId, t.StepOrder
"@
$conn.Close()

$byHadith = @{}
foreach ($l in $links.Rows) {
    $k = [string]$l.HadithId
    if (-not $byHadith.ContainsKey($k)) { $byHadith[$k] = New-Object System.Collections.ArrayList }
    [void]$byHadith[$k].Add([ordered]@{
        step = $l.StepOrder; term = [string]$l.TransmissionTerm
        student = [string]$l.Student; studentItqan = $l.StudentItqan
        sheikh = [string]$l.Sheikh; sheikhItqan = $l.SheikhItqan; sheikhGrade = [string]$l.SheikhGrade
    })
}

$result = foreach ($h in $hadiths.Rows) {
    $k = [string]$h.Id
    [ordered]@{
        id = $k; book = [string]$h.BookName; number = $h.HadithNumber; head = [string]$h.Head
        links = @(if ($byHadith.ContainsKey($k)) { $byHadith[$k] } else { @() })
    }
}
[System.IO.File]::WriteAllText($Out, ($result | ConvertTo-Json -Depth 5 -Compress), (New-Object System.Text.UTF8Encoding $false))
"hadiths: $($hadiths.Rows.Count)  links: $($links.Rows.Count)  -> $Out"
