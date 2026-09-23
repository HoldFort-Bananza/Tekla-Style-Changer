using System;
using System.Collections.Generic;
using Tekla.Structures;
using Tekla.Structures.Drawing;
using Tekla.Structures.DrawingInternal;

namespace StyleChanger
{
    /// <summary>
    /// Rdzeń logiki: znajdź widok z zaznaczenia w edytorze i podmień mu
    /// styl (nazwany plik atrybutów widoku) na wskazany.
    /// </summary>
    internal class StyleChangerService
    {
        // Rozszerzenie plików stylu widoku w Tekli - ustalone empirycznie
        // (DiagRunner --diag-active, 2026-09-23, na modelu Style_2D): pliki
        // *.vi w katalogu attributes modelu/firmy. GetFileDictionaryByExtension
        // chce nazwę rozszerzenia BEZ kropki ("vi") - z kropką zwraca 0 wyników.
        private const string ViewStyleExtension = "vi";

        // Domyślny styl - dokładnie ten, który operator dziś wybiera ręcznie
        // z rozwijanej listy w oknie "View properties" dla widoków z
        // sąsiednią częścią (patrz zrzut ekranu w AGENTS.md). Nazwa
        // potwierdzona na żywym modelu: plik
        // W_View_Railing_Neighbour.vi istnieje i wczytuje się poprawnie
        // (Scale=10, ViewExtensionForNeighbourParts=50).
        public const string DefaultStyleName = "W_View_Railing_Neighbour";

        /// <summary>
        /// Lista dostępnych stylów widoku (nazwa bez rozszerzenia -&gt; pełna
        /// ścieżka pliku .vi), do wypełnienia listy wyboru w UI.
        /// </summary>
        public IReadOnlyDictionary<string, string> GetAvailableStyles(string modelPath)
        {
            return TeklaStructuresFiles.GetFileDictionaryByExtension(ViewStyleExtension, modelPath);
        }

        /// <summary>
        /// Znajduje WSZYSTKIE widoki powiązane z aktualnym zaznaczeniem w
        /// edytorze: zaznaczony obiekt to albo sam widok, albo coś w środku
        /// widoku (np. wymiar, część) - wtedy bierzemy widok, w którym to
        /// leży. Operator może zaznaczyć kilka widoków (albo obiekty w kilku
        /// różnych widokach) naraz - każdy trafia do wyniku raz
        /// (deduplikacja po Identifier.GUID, bo GetView() może zwrócić nowy
        /// obiekt-wrapper dla tego samego widoku co bezpośrednie zaznaczenie).
        /// </summary>
        public List<View> ResolveTargetViews(DrawingObjectEnumerator selected)
        {
            var views = new List<View>();
            var seenGuids = new HashSet<Guid>();

            while (selected.MoveNext())
            {
                var obj = selected.Current;
                var view = obj is View v ? v : obj.GetView() as View;
                if (view == null)
                {
                    continue;
                }

                if (seenGuids.Add(view.GetIdentifier().GUID))
                {
                    views.Add(view);
                }
            }

            return views;
        }

        /// <summary>
        /// Aplikuje nazwany styl (plik .vi) na wskazane widoki i zapisuje
        /// zmianę JEDNYM CommitChanges() dla całego rysunku. Zwraca liczbę
        /// widoków, na które styl faktycznie się zaaplikował - niepowodzenia
        /// pojedynczych widoków trafiają do logu i nie przerywają reszty.
        /// </summary>
        public int ApplyStyleToViews(Drawing drawing, IReadOnlyList<View> views, string styleName, Action<string> log)
        {
            int modified = 0;
            foreach (var view in views)
            {
                try
                {
                    view.Attributes = new View.ViewAttributes(styleName);
                }
                catch (Exception ex)
                {
                    log($"Nie udało się wczytać stylu \"{styleName}\" dla widoku \"{view.Name}\": {ex.Message}");
                    continue;
                }

                if (!view.Modify())
                {
                    log($"Widok.Modify() zwróciło false dla widoku \"{view.Name}\" - pomijam.");
                    continue;
                }

                modified++;
            }

            if (modified == 0)
            {
                return 0;
            }

            // TransactionManager nie dotyczy obiektów rysunkowych - zmianę
            // utrwala CommitChanges() na rysunku, patrz ../CLAUDE.md. Jedno
            // wywołanie na koniec zapisuje wszystkie zmodyfikowane widoki
            // naraz, zamiast commitować po każdym z osobna.
            if (!drawing.CommitChanges())
            {
                log("Drawing.CommitChanges() zwróciło false - zmiany mogły nie zostać zapisane.");
                return 0;
            }

            return modified;
        }
    }
}
