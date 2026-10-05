# Phase 6: export every hadith of a database with its chain, as JSON Lines (one file per book).
# Works for both the v2 database (narrators keyed by ItqanId) and the Shamela one (keyed by SourceKey).
#   export_db_chains.ps1 -Database SmartHadithTree -Out data\shamela_rijal\phase6\v2
#   export_db_chains.ps1 -Database SmartHadithTree_Shamela -Out data\shamela_rijal\phase6\shamela
# A line: {"id","book","number","head" (first 400 chars of MatnArabic),"links":[[step,sheikhKey,studentKey,term,sheikhName,studentName]]}
param(
    [Parameter(Mandatory)][string]$Database,
    [Parameter(Mandatory)][string]$Out
)

New-Item -ItemType Directory -Force $Out | Out-Null
$keyOf = if ($Database -like '*Shamela*') { { param($a) "$a.SourceKey" } } else { { param($a) "CAST($a.ItqanId AS nvarchar(20))" } }
$shKey = & $keyOf 'sh'
$stKey = & $keyOf 'st'

$conn = New-Object System.Data.SqlClient.SqlConnection "Server=.;Database=$Database;Integrated Security=True;TrustServerCertificate=True"
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandTimeout = 0
$cmd.CommandText = @"
SELECT h.Id, h.BookName, h.HadithNumber, LEFT(h.MatnArabic, 400) AS Head,
       t.StepOrder, t.TransmissionTerm,
       $shKey AS SheikhKey, sh.FullName AS SheikhName,
       $stKey AS StudentKey, st.FullName AS StudentName
FROM Hadiths h
LEFT JOIN Transmissions t ON t.HadithId = h.Id
LEFT JOIN Narrators sh ON sh.Id = t.SheikhId
LEFT JOIN Narrators st ON st.Id = t.StudentId
ORDER BY h.BookName, h.Id, t.StepOrder
"@
$reader = $cmd.ExecuteReader()

$utf8 = New-Object System.Text.UTF8Encoding($false)
$opts = New-Object System.Text.Json.JsonSerializerOptions
$opts.Encoder = [System.Text.Encodings.Web.JavaScriptEncoder]::UnsafeRelaxedJsonEscaping

$writer = $null; $curBook = $null; $cur = $null; $count = 0
$bookIndex = 0
function Flush {
    if ($null -ne $script:cur) {
        $script:writer.WriteLine([System.Text.Json.JsonSerializer]::Serialize($script:cur, $script:opts))
        $script:count++
    }
}
function Str($v) { if ($v -is [DBNull]) { $null } else { [string]$v } }

while ($reader.Read()) {
    $id = [string]$reader['Id']
    if ($null -eq $cur -or $cur['id'] -ne $id) {
        Flush
        $book = [string]$reader['BookName']
        if ($book -ne $curBook) {
            if ($null -ne $writer) { $writer.Dispose() }
            $bookIndex++
            $writer = New-Object System.IO.StreamWriter((Join-Path $Out ("{0:D2}.jsonl" -f $bookIndex)), $false, $utf8)
            $curBook = $book
        }
        $cur = [System.Collections.Generic.Dictionary[string,object]]::new()
        $cur['id'] = $id; $cur['book'] = $book
        $cur['number'] = $(if ($reader['HadithNumber'] -is [DBNull]) { $null } else { [int]$reader['HadithNumber'] })
        $cur['head'] = [string]$reader['Head']
        $cur['links'] = [System.Collections.Generic.List[object]]::new()
    }
    if ($reader['StepOrder'] -isnot [DBNull]) {
        $cur['links'].Add([object[]]@([int]$reader['StepOrder'], (Str $reader['SheikhKey']), (Str $reader['StudentKey']),
                               (Str $reader['TransmissionTerm']), (Str $reader['SheikhName']), (Str $reader['StudentName'])))
    }
}
Flush
if ($null -ne $writer) { $writer.Dispose() }
$reader.Close(); $conn.Close()
"Exported $count hadiths of $bookIndex books from $Database to $Out"


