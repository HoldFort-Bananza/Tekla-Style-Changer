using System;
using System.Collections.Generic;
using System.IO;
using Tekla.Structures;
using Tekla.Structures.Drawing;
using Tekla.Structures.Model;

namespace StyleChanger
{
    // Narzędzie diagnostyczne, czysto do odczytu - nic nie zmienia w rysunku
    // ani w modelu. Cel: ustalić na ŻYWEJ Tekli (nie z dokumentacji, nie "na
    // wyczucie") prawdziwą nazwę/rozszerzenie plików ze stylami widoku oraz
    // sprawdzić, co realnie zwraca zaznaczenie w edytorze, zanim napiszemy
    // logikę aplikowania stylu w StyleChangerService.
    internal static class DiagRunner
    {
        public static void RunOnActiveDrawing()
        {
            void Log(string s) => Console.WriteLine(s);

            var dh = new DrawingHandler();
            Log($"DrawingHandler.GetConnectionStatus(): {dh.GetConnectionStatus()}");
            if (!dh.GetConnectionStatus())
            {
                Log("Brak połączenia z Teklą.");
                return;
            }

            var drawing = dh.GetActiveDrawing();
            if (drawing == null)
            {
                Log("Brak otwartego rysunku.");
                return;
            }
            Log($"Aktywny rysunek: {drawing.Mark} / {drawing.Name}");

            // Zaznaczenie w edytorze rysunku - to ma być wyzwalacz przycisku
            // w appce docelowej: operator klika widok (albo dowolny obiekt
            // w widoku), potem klika jeden przycisk.
            var selector = dh.GetDrawingObjectSelector();
            var selected = selector.GetSelected();
            int count = 0;
            while (selected.MoveNext())
            {
                count++;
                var obj = selected.Current;
                if (obj is View directView)
                {
                    Log($"  Zaznaczony obiekt #{count}: View bezpośrednio, nazwa=\"{directView.Name}\"");
                    DumpAttributes("    ", directView.Attributes, Log);
                    continue;
                }

                var containingView = obj.GetView() as View;
                Log(containingView != null
                    ? $"  Zaznaczony obiekt #{count}: {obj.GetType().Name}, widok zawierający=\"{containingView.Name}\""
                    : $"  Zaznaczony obiekt #{count}: {obj.GetType().Name}, GetView() nie zwrócił widoku typu View");
            }
            if (count == 0)
            {
                Log("  Nic nie jest zaznaczone w edytorze rysunku - zaznacz widok albo obiekt w widoku i uruchom ponownie.");
            }

            // GetStandardPropertyFileDirectories() jest internal w tej wersji
            // API (sprawdzone refleksją na 2025.0.0) - jedyna publiczna droga
            // to skonstruować TeklaStructuresFiles z modelPath i przeczytać
            // właściwość PropertyFileDirectories, którą konstruktor sam
            // wypełnia.
            string modelPath = null;
            try
            {
                var model = new Model();
                if (model.GetConnectionStatus())
                {
                    modelPath = model.GetInfo().ModelPath;
                }
            }
            catch (Exception ex)
            {
                Log("Nie udało się pobrać ModelPath z modelu: " + ex.Message);
            }
            Log($"ModelPath: {modelPath ?? "(brak)"}");

            List<string> dirs;
            try
            {
                var files = new TeklaStructuresFiles(modelPath);
                dirs = files.PropertyFileDirectories;
            }
            catch (Exception ex)
            {
                Log("Błąd TeklaStructuresFiles/PropertyFileDirectories: " + ex.Message);
                return;
            }

            Log($"Katalogi plików właściwości ({dirs.Count}):");
            foreach (var dir in dirs)
            {
                Log("  " + dir);
                if (!Directory.Exists(dir))
                {
                    Log("    (katalog nie istnieje)");
                    continue;
                }

                string[] matches;
                try
                {
                    // "*view*" na Windows jest niewrażliwe na wielkość liter,
                    // więc łapie i "View", i "view".
                    matches = Directory.GetFiles(dir, "*view*", SearchOption.TopDirectoryOnly);
                }
                catch (Exception ex)
                {
                    Log("    Błąd odczytu katalogu: " + ex.Message);
                    continue;
                }

                foreach (var f in matches)
                {
                    Log("    " + Path.GetFileName(f));
                }
            }

            // Test właściwej metody API do listowania stylów widoku - ustalone
            // z powyższej listy katalogów, że pliki stylu widoku mają
            // rozszerzenie ".vi" (np. W_View_Railing_Neighbour.vi). Sprawdzamy
            // obiema wersjami (z kropką i bez), bo dokumentacja XML nie mówi,
            // czego dokładnie oczekuje GetFileDictionaryByExtension.
            try
            {
                foreach (var ext in new[] { "vi", ".vi" })
                {
                    var dict = TeklaStructuresFiles.GetFileDictionaryByExtension(ext, modelPath);
                    Log($"GetFileDictionaryByExtension(\"{ext}\", modelPath): {dict.Count} wpisów, zawiera \"W_View_Railing_Neighbour\": {dict.ContainsKey("W_View_Railing_Neighbour")}");
                }
            }
            catch (Exception ex)
            {
                Log("Błąd GetFileDictionaryByExtension: " + ex.Message);
            }

            // Bezpieczne: konstruktor tylko WCZYTUJE plik z dysku do nowego
            // obiektu w pamięci, nic w rysunku/modelu się nie zmienia -
            // Modify()/CommitChanges() nigdzie tu nie wołane.
            try
            {
                var attrs = new View.ViewAttributes("W_View_Railing_Neighbour");
                Log($"Wczytano W_View_Railing_Neighbour.vi: Scale={attrs.Scale}, ViewExtensionForNeighbourParts={attrs.ViewExtensionForNeighbourParts}");
            }
            catch (Exception ex)
            {
                Log("Błąd wczytania W_View_Railing_Neighbour.vi: " + ex.Message);
            }
        }

        // Pełny zrzut pól ViewAttributes, które mogłyby wpływać na pozycję
        // widoku na arkuszu (LocationBy, FixedViewPlacing, PartialProfile*,
        // DatumLevel) obok pól, które już wiadomo, że są "sąsiedzkie"
        // (Scale, ViewExtensionForNeighbourParts) - do znalezienia, które
        // POLE faktycznie przesunęło część, zamiast zgadywać.
        private static void DumpAttributes(string indent, View.ViewAttributes a, Action<string> log)
        {
            log($"{indent}Scale={a.Scale}");
            log($"{indent}ViewExtensionForNeighbourParts={a.ViewExtensionForNeighbourParts}");
            log($"{indent}LocationBy={a.LocationBy}");
            log($"{indent}FixedViewPlacing={a.FixedViewPlacing}");
            log($"{indent}DatumLevel={a.DatumLevel}");
            log($"{indent}PartialProfileLength={a.PartialProfileLength}");
            log($"{indent}PartialProfileOffset={a.PartialProfileOffset}");
            log($"{indent}ReflectedView={a.ReflectedView}");
            log($"{indent}UndeformedView={a.UndeformedView}");
            log($"{indent}UnfoldedView={a.UnfoldedView}");
            log($"{indent}PourView={a.PourView}");
            log($"{indent}ShowPartOpeningsOrRecessSymbol={a.ShowPartOpeningsOrRecessSymbol}");
            log($"{indent}ViewPlaneDatumPointForElevations={a.ViewPlaneDatumPointForElevations}");
            log($"{indent}LabelPositionHorizontal={a.LabelPositionHorizontal}");
            log($"{indent}LabelPositionVertical={a.LabelPositionVertical}");
        }

        // Wczytuje nazwany plik .vi do izolowanego obiektu w pamięci (nic nie
        // zmienia w rysunku) i wypisuje te same pola co DumpAttributes - do
        // porównania ze stanem widoku przed/po kliknięciu przycisku.
        public static void DumpStyleFile(string styleName)
        {
            void Log(string s) => Console.WriteLine(s);
            try
            {
                var attrs = new View.ViewAttributes(styleName);
                Log($"Zawartość pliku {styleName}.vi:");
                DumpAttributes("  ", attrs, Log);
            }
            catch (Exception ex)
            {
                Log($"Błąd wczytania {styleName}.vi: " + ex.Message);
            }
        }

        // Test regresyjny na innym rysunku niż [35099] - żeby sprawdzić, że
        // apka nie jest przypadkiem "zahardcodowana" pod jeden konkretny
        // rysunek. W PRZECIWIEŃSTWIE do reszty tego pliku, TO REALNIE ZMIENIA
        // RYSUNEK (Modify()+CommitChanges()) - świadomie, na wyraźną prośbę
        // (2026-09-23): "znajdź inny rysunek z neighborami i sprawdź czy
        // nadal działa". Zmiana stylu widoku jest odwracalna w Tekli
        // (Ctrl+Z), więc ryzyko niskie - w odróżnieniu od RO Axis Dimension
        // Remover, gdzie DiagRunner NIGDY nie robi realnych zmian.
        public static void TestOnOtherDrawing()
        {
            void Log(string s) => Console.WriteLine(s);

            var dh = new DrawingHandler();
            if (!dh.GetConnectionStatus())
            {
                Log("Brak połączenia z Teklą.");
                return;
            }

            var current = dh.GetActiveDrawing();
            var currentMark = current?.Mark;
            Log($"Aktualnie aktywny rysunek: {currentMark ?? "(brak)"}");

            // Krok 1: metadane wszystkich rysunków (Mark/Name) BEZ otwierania -
            // szukamy kandydatów po nazwie (ten sam rodzinny wzorzec co
            // [35099] "Einzelteil Geländer", które miało widok w stylu
            // Neighbour).
            var candidates = new List<Drawing>();
            var drawings = dh.GetDrawings();
            int total = 0;
            while (drawings.MoveNext())
            {
                total++;
                var d = drawings.Current;
                if (string.Equals(d.Mark, currentMark, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (d.Name != null && d.Name.IndexOf("Geländer", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    candidates.Add(d);
                }
            }
            Log($"Rysunków w modelu: {total}, kandydatów (Name zawiera \"Geländer\", pomijając aktywny): {candidates.Count}");

            Drawing target = null;
            View targetView = null;

            foreach (var candidate in candidates)
            {
                dh.SetActiveDrawing(candidate, true);
                var top = candidate.GetSheet().GetAllObjects();
                View firstView = null;
                while (top.MoveNext())
                {
                    if (top.Current is View v)
                    {
                        firstView = v;
                        break;
                    }
                }
                if (firstView == null)
                {
                    Log($"  [{candidate.Mark}] {candidate.Name}: brak widoku na arkuszu, pomijam.");
                    continue;
                }

                Log($"  [{candidate.Mark}] {candidate.Name}: widok znaleziony, Scale={firstView.Attributes.Scale}, ViewExtensionForNeighbourParts={firstView.Attributes.ViewExtensionForNeighbourParts} - WYBRANY do testu.");
                target = candidate;
                targetView = firstView;
                break;
            }

            if (target == null || targetView == null)
            {
                Log("Nie znaleziono innego rysunku z widokiem do przetestowania.");
                return;
            }

            var selector = dh.GetDrawingObjectSelector();
            selector.SelectObject(targetView);

            var service = new StyleChangerService();
            var modified = service.ApplyStyleToViews(target, new List<View> { targetView }, StyleChangerService.DefaultStyleName, Log);
            var ok = modified > 0;
            Log(ok
                ? $"OK: styl \"{StyleChangerService.DefaultStyleName}\" zastosowany na [{target.Mark}] {target.Name}."
                : $"NIEUDANE zastosowanie stylu na [{target.Mark}] {target.Name} - patrz log wyżej.");

            if (ok)
            {
                Log($"Weryfikacja po zapisie: Scale={targetView.Attributes.Scale}, ViewExtensionForNeighbourParts={targetView.Attributes.ViewExtensionForNeighbourParts} (oczekiwane: 10 i 50, jak w W_View_Railing_Neighbour.vi).");
            }
        }
    }
}
