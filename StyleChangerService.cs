using System;
using System.Collections.Generic;
using Tekla.Structures;
using Tekla.Structures.Drawing;

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
        /// Znajduje widok powiązany z aktualnym zaznaczeniem w edytorze:
        /// albo sam zaznaczony obiekt jest widokiem, albo bierzemy widok,
        /// w którym leży pierwszy zaznaczony obiekt innego typu (np. wymiar).
        /// </summary>
        public View ResolveTargetView(DrawingObjectEnumerator selected)
        {
            while (selected.MoveNext())
            {
                var obj = selected.Current;
                if (obj is View directView)
                {
                    return directView;
                }

                if (obj.GetView() is View containingView)
                {
                    return containingView;
                }
            }

            return null;
        }

        /// <summary>
        /// Aplikuje nazwany styl (plik .vi) na wskazany widok i zapisuje
        /// zmianę. Zwraca false przy niepowodzeniu - powód trafia do logu,
        /// nie jest przełykany po cichu.
        /// </summary>
        public bool ApplyStyle(Drawing drawing, View view, string styleName, Action<string> log)
        {
            try
            {
                view.Attributes = new View.ViewAttributes(styleName);
            }
            catch (Exception ex)
            {
                log($"Nie udało się wczytać stylu \"{styleName}\": {ex.Message}");
                return false;
            }

            if (!view.Modify())
            {
                log("Widok.Modify() zwróciło false - zmiana nie została zastosowana.");
                return false;
            }

            // TransactionManager nie dotyczy obiektów rysunkowych - zmianę
            // utrwala CommitChanges() na rysunku, patrz ../CLAUDE.md.
            if (!drawing.CommitChanges())
            {
                log("Drawing.CommitChanges() zwróciło false - zmiana mogła nie zostać zapisana.");
                return false;
            }

            return true;
        }
    }
}
