# LOD0-Mesher – Probedurchlauf der Benchmark-Pipeline

Datum: 2026-11-10. Auftrag: LOD0 optimieren, Eingaben, Aufruf und Mesh-Ausgabe identisch erhalten. Der Benutzer hat Hintergrundlast bereits minimiert.

## Ablauf

1. Ausgangszustand geprüft. Die vorherige Pipeline-Bereinigung war noch uncommittet. Vor der Baseline werden diese Änderungen als eigene Ausgangsrevision gesichert.
2. Vollständige Tests mit `dotnet test` ausgeführt. 388 bestanden, keine Fehler oder übersprungenen Tests.
3. Baseline mit `dotnet run -c Release --project BlockGame.Benchmarks -- --mode Measure --case 'mesh/*' --output artifacts/lod0-trial/baseline` speichern.
4. Hypothese: Die 16 einzelnen List-Add-Aufrufe pro Vertex lassen sich durch einen zusammenhängenden Schreibzugriff ersetzen. Änderungen werden auf LOD0 beschränkt. Byte-Reihenfolge, Padding, AO, Texturlayer und Indizes müssen identisch bleiben.
5. Kandidat mit `dotnet test` geprüft. Wieder 388 bestanden, keine Fehler oder übersprungenen Tests. Rider-MCP `get_file_problems` meldete für die geänderte Datei keine Fehler. Kandidat committet, auf sauberem Arbeitsbaum gemessen und die gespeicherten Ordner verglichen.

Die Rolle eines Optimierungs-Workers wurde in diesem Chat übernommen. Es wurde ein Kandidat entwickelt und gemessen.

## Revisionen und Befehle

- Baseline: `be6bdbf732bf579e82e28975e14a89a7ef3dc289`.
- Kandidat: `3d8dc36c2c2e52f858a2fdf749f57ae86cc1cf36`.
- Beide Läufe haben denselben Harness-Tree `a2c571cefff70eb671cb784fee37cc3ccbcced29` und dieselbe Environment-Identität. Der Vergleich enthält keine Harness-Warnung.
- Zwischen den beiden Revisionen wurde ausschließlich `BlockGame.Core/Terrain/Meshing/Lod0Mesher.cs` geändert, 18 hinzugefügte Zeilen.

Aus dem Repository-Root wurden nach dem jeweiligen vollständigen Testlauf und Commit folgende Befehle ausgeführt. Launcher-Ausgaben wurden in den benachbarten Dateien `baseline-launch.log` und `candidate-launch.log` gespeichert.

```powershell
dotnet run -c Release --project BlockGame.Benchmarks -- --mode Measure --case 'mesh/*' --output artifacts/lod0-trial/baseline
dotnet run -c Release --project BlockGame.Benchmarks -- --mode Measure --case 'mesh/*' --output artifacts/lod0-trial/candidate
dotnet run --project BlockGame.Benchmarks -- compare artifacts/lod0-trial/baseline artifacts/lod0-trial/candidate artifacts/lod0-trial/comparison.json
```

Beide Measure-Läufe und der Vergleich endeten mit Exitcode 0. BenchmarkDotNet meldete insgesamt 145,74 Sekunden für die Baseline und 153,38 Sekunden für den Kandidaten, einschließlich Benchmark-Build. Eine kürzere Operation führt wegen automatischer Kalibrierung nicht zwingend zu einem kürzeren gesamten Benchmark-Lauf.

## Änderung und Korrektheit

LOD0 besitzt jetzt einen eigenen privaten `AddVertex`-Schreibpfad. `CollectionsMarshal.SetCount` erweitert die vorhandene gepoolte Liste um 16 Bytes und stellt bei Bedarf Kapazität bereit. Erst danach wird ein kurzlebiger Span auf den neuen Bereich genommen. `BinaryPrimitives` schreibt die drei Floats und den Texture-Layer explizit little-endian. AO und das Null-Padding werden ebenfalls vollständig geschrieben. Anschließend wird der Vertex-Zähler erhöht.

Dadurch entfallen 16 einzelne List-Add-Aufrufe je Vertex. Die gespeicherten Felder und ihre Reihenfolge entsprechen dem bisherigen Code. Der Span überlebt keine weitere Listenänderung. Der bestehende Mesher-Aufruf, die Face-Reihenfolge, AO-Berechnung, Diagonalwahl, Indizes und Pool-Rückgabe sind identisch. Es wurden keine zusätzlichen Compilerattribute gesetzt. Die bereits vorhandenen Inlining-Einstellungen gelten weiterhin.

Zusätzlich zur Testsuite wurden die Szenario-Input-Fingerprints und die Output-Fingerprints für alle sieben Baseline-/Kandidatenpaare abgeglichen. Alle stimmen überein. Die Output-Prüfung erfasst sämtliche Vertexbytes einschließlich Padding und sämtliche Indexbytes. Die vorhandenen Tests prüfen unter anderem Materialien, AO, Diagonalwahl, Winding, unveränderte Eingaben und Pool-Zustand über wiederholte Builds.

Die Gleichheit ist für die gemessenen Fixtures und vorhandenen Tests nachgewiesen. Sie ist kein erschöpfender Test aller möglichen Chunk-Konfigurationen. Die Änderung beschränkt sich auf die Darstellung bereits berechneter Vertexwerte.

## Beobachtungen zum Setup

- Eine Messung erfordert einen sauberen Arbeitsbaum. Auch dieses Protokoll muss vor Messungen committet sein. Während der Messung werden Notizen nur unter dem ignorierten `artifacts/` gesammelt und anschließend hier eingetragen.
- Die vollständige Testsuite wird manuell ausgeführt. Das Benchmark-Setup bestätigt diesen Schritt nicht automatisch.
- Die Pipeline ließ sich vollständig mit den drei manuellen CLI-Einstiegspunkten bedienen. Es waren keine PowerShell-Wrapper, ZIP-Dateien oder zusätzlichen Nachweisdateien nötig.
- Die Ausführung erfolgte über die freigegebene Shell außerhalb der Sandbox, damit MSBuild und BenchmarkDotNet ihre Prozesse starten können. Es trat in diesem Durchlauf kein Ausführungsfehler auf.
- Rider meldete keine Dateifehler. Der CLI-Build zeigte weiterhin bestehende Nullable-Warnungen im Mesher und anderen Engine-Dateien. Diese erscheinen auch beim Debug-Build durch `compare` und machen dessen Ausgabe länger.
- Ein bestehender Ergebnisordner muss für einen weiteren Lauf durch einen neuen Namen ersetzt werden. Die getrennten Ordner verhinderten hier ein Überschreiben der Baseline.
- `compare` funktioniert ohne `-c Release`, baut aber dafür gegebenenfalls die Debug-Projekte neu. Das erzeugt zusätzliche Buildausgabe, obwohl nur Ergebnisdateien ausgewertet werden.
- Der Markdown-Vergleich hat 295 Zeilen. Die vollständigen Umgebungsdaten einschließlich Content-/Native-Abhängigkeiten werden für beide Seiten ausgeschrieben. Die eigentliche Ergebnistabelle ist dadurch schwerer zu finden. Eine kompakte Umgebungstabelle wäre lesbarer, die vollständigen Details könnten im JSON verbleiben.
- Der Markdown-Vergleich zeigt Mittelwerte und Urteil, aber weder Prozentänderung noch Allokationsdifferenz in seiner Tabelle. Diese Werte stehen im Vergleichs-JSON. Für die folgende Zusammenfassung wurden sie daraus entnommen.
- Die Mesh-Familie enthält drei Kontrollen ohne ausgegebene Geometrie. Auch sie durchlaufen das normale Sampling. `null-air` liegt unter dem 100-ns-Floor und erhält korrekt kein Prozenturteil. Die beiden anderen Kontrollen bleiben innerhalb des Unsicherheitsbudgets.
- Der Benchmark misst Builds mit aufgewärmten Listenpools und Rückgabe der Puffer innerhalb der Operation. Er erfasst damit diesen CPU-Pfad. Kalte Pool-Vergrößerung, Uploads und Frame-Zeiten werden durch diese Messung nicht bewertet.
- Das Energieprofil wurde gemäß Workflow weder verändert noch automatisch erfasst. Hintergrundlast wurde vom Benutzer minimiert. OS, CPU, Runtime und Job sind in den Ergebnissen gespeichert.

Diese Beobachtungen wurden protokolliert. Während des Vergleichs wurde das Harness nicht verändert.

## Ergebnisse

Umgebung für beide Läufe: AMD Ryzen 9 7900X, 12 physische / 24 logische Kerne, Windows 11 25H2 (10.0.26200.9457), .NET SDK 10.0.103, Runtime 10.0.3, BenchmarkDotNet 0.15.8, X64 RyuJIT, Concurrent Workstation GC.

| Fall | Baseline µs | Kandidat µs | Zeitänderung | Urteil |
| --- | ---: | ---: | ---: | --- |
| allocated-air | 1,3310 | 1,3314 | +0,03 % | inconclusive |
| checkerboard | 1437,016 | 456,079 | −68,26 % | improved |
| exposed-solid | 210,405 | 90,562 | −56,96 % | improved |
| floor | 134,394 | 54,037 | −59,79 % | improved |
| frozen-terrain | 30,784 | 18,388 | −40,27 % | improved |
| null-air | 0,02541 | 0,02575 | unter Timing-Floor | inconclusive |
| occluded-solid | 12,661 | 12,688 | +0,21 % | inconclusive |

Managed Allocations betragen auf beiden Seiten in jedem Fall 408 Byte pro Operation. Alle vier Fälle mit ausgegebener Geometrie werden vom Comparator als verbessert bewertet. Für die drei Kontrollen wird keine Beschleunigung beansprucht.

Das Ergebnis stammt aus einem Baseline-/Kandidatenpaar auf dieser Maschine. Es wurden keine unabhängigen Wiederholungen mit vertauschter Reihenfolge durchgeführt. Die Prozentwerte beschreiben die gemessene CPU-Buildzeit der jeweiligen Fixtures und lassen sich nicht unmittelbar in FPS übersetzen.

## Aufbewahrte Dateien

- `artifacts/lod0-trial/baseline/` enthält Baseline-Berichte, Rohmessungen, Manifest und Kindprozessdaten.
- `artifacts/lod0-trial/candidate/` enthält dieselben Daten für den Kandidaten.
- `artifacts/lod0-trial/comparison.json` und `comparison.md` enthalten den Vergleich.
- `artifacts/lod0-trial/candidate-tests.log` enthält den vollständigen Kandidaten-Testlauf. Der Baseline-Testlauf ist im Chat-Toolprotokoll dokumentiert.
- `artifacts/lod0-trial/baseline-launch.log` und `candidate-launch.log` enthalten die Benchmark-Ausgaben.

Die Ergebnisdateien bleiben lokal unter dem ignorierten `artifacts/`. Dieses ausdrücklich angeforderte Versuchsprotokoll ergänzt die allgemeinen Arbeitsregeln in `docs/benchmarking.md`.
