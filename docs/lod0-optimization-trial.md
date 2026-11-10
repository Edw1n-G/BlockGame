# LOD0-Mesher – Probedurchlauf der Benchmark-Pipeline

Datum: 2026-11-10. Auftrag: LOD0 optimieren, Eingaben, Aufruf und Mesh-Ausgabe identisch erhalten. Der Benutzer hat Hintergrundlast bereits minimiert.

## Ablauf

1. Ausgangszustand geprüft. Die vorherige Pipeline-Bereinigung war noch uncommittet. Vor der Baseline werden diese Änderungen als eigene Ausgangsrevision gesichert.
2. Vollständige Tests mit `dotnet test` ausführen.
3. Baseline mit `dotnet run -c Release --project BlockGame.Benchmarks -- --mode Measure --case 'mesh/*' --output artifacts/lod0-trial/baseline` speichern.
4. Hypothese: Die 16 einzelnen List-Add-Aufrufe pro Vertex lassen sich durch einen zusammenhängenden Schreibzugriff ersetzen. Änderungen werden auf LOD0 beschränkt. Byte-Reihenfolge, Padding, AO, Texturlayer und Indizes müssen identisch bleiben.
5. Kandidat testen, committen und mit unverändertem Harness und gleichen Fällen messen. Anschließend die gespeicherten Ordner vergleichen.

## Beobachtungen zum Setup

- Eine Messung erfordert einen sauberen Arbeitsbaum. Auch dieses Protokoll muss vor Messungen committet sein. Während der Messung werden Notizen nur unter dem ignorierten `artifacts/` gesammelt und anschließend hier eingetragen.
- Die vollständige Testsuite wird manuell ausgeführt. Das Benchmark-Setup bestätigt diesen Schritt nicht automatisch.

## Ergebnisse

Der Durchlauf ist in Arbeit. Ergebnisse und Einschränkungen werden nach den Messungen ergänzt.
