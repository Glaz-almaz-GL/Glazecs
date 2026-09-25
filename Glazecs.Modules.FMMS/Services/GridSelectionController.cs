using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;

namespace Glazecs.Modules.FMMS.Services
{
    /// <summary>
    /// Выделение строк MudDataGrid по правилам настольной таблицы (DataGridView, Проводник):
    /// щелчок, Ctrl+щелчок, Shift+щелчок, рамка выделения мышью, Ctrl+A, Esc, стрелки, Home/End.
    /// </summary>
    /// <remarks>
    /// Порядок строк берётся из <see cref="MudDataGrid{T}.FilteredItems"/> — отфильтрованном и отсортированном,
    /// то есть таком, как их видит пользователь. Сетке нужно выставить <c>SelectOnRowClick="false"</c>:
    /// щелчки по строкам обрабатывает этот класс через JS-модуль <c>js/gridSelection.js</c>.
    /// </remarks>
    /// <typeparam name="T">Тип строки таблицы.</typeparam>
    public sealed class GridSelectionController<T> : IAsyncDisposable where T : struct
    {
        #region Fields

        private const string ModulePath = "./_content/Glazecs.Modules.FMMS/js/gridSelection.js";

        private readonly Func<T, int> _keySelector;
        private readonly Func<Task> _onChanged;
        private MudDataGrid<T>? _grid;
        private IJSObjectReference? _module;
        private IJSObjectReference? _handle;
        private DotNetObjectReference<GridSelectionController<T>>? _selfRef;
        private HashSet<T> _areaBase = [];
        private int? _pendingScrollKey;

        #endregion

        /// <param name="keySelector">Уникальный ключ строки; попадает в CSS-класс строки для JS.</param>
        /// <param name="onChanged">Вызывается, когда выделение изменилось вне обработчика событий Blazor (из JS).</param>
        public GridSelectionController(Func<T, int> keySelector, Func<Task> onChanged)
        {
            _keySelector = keySelector;
            _onChanged = onChanged;
        }

        #region State

        /// <summary>
        /// Выделенные строки. При каждом изменении — новый экземпляр: MudDataGrid замечает смену только по ссылке.
        /// </summary>
        public HashSet<T> Selected { get; private set; } = [];

        /// <summary>
        /// Опорная строка диапазона для Shift.
        /// </summary>
        public T? Anchor { get; private set; }

        /// <summary>
        /// Текущая строка — последняя, на которую пришёл щелчок или стрелка.
        /// </summary>
        public T? Cursor { get; private set; }

        #endregion

        #region Selection Logic

        /// <summary>
        /// Щелчок по строке: без модификаторов — только она; Ctrl — переключить её;
        /// Shift — диапазон от опорной строки; Ctrl+Shift — добавить диапазон к выделению.
        /// </summary>
        public void Click(T item, bool ctrl, bool shift, IReadOnlyList<T> ordered)
        {
            if (shift && Anchor.HasValue)
            {
                HashSet<T> range = GetRange(Anchor.Value, item, ordered);

                if (ctrl)
                {
                    range.UnionWith(Selected);
                }

                Selected = range;
                Cursor = item;
                return;
            }

            if (ctrl)
            {
                HashSet<T> next = [.. Selected];

                if (!next.Remove(item))
                {
                    next.Add(item);
                }

                Selected = next;
            }
            else
            {
                Selected = [item];
            }

            Anchor = item;
            Cursor = item;
        }

        /// <summary>
        /// Стрелки, Home, End: переносят текущую строку в пределах страницы; с Shift — расширяют диапазон.
        /// </summary>
        /// <returns>Строка, ставшая текущей, или <c>null</c>, если страница пуста.</returns>
        public T? Move(GridNavigation navigation, bool shift, IReadOnlyList<T> pageItems, IReadOnlyList<T> ordered)
        {
            if (pageItems.Count == 0)
            {
                return null;
            }

            int index = Cursor.HasValue ? IndexOf(pageItems, Cursor.Value) : -1;
            int lastIndex = pageItems.Count - 1;

            int target = navigation switch
            {
                GridNavigation.Up => index < 0 ? 0 : index - 1,
                GridNavigation.Down => index < 0 ? 0 : index + 1,
                GridNavigation.Home => 0,
                GridNavigation.End => lastIndex,
                _ => index
            };

            T item = pageItems[Math.Clamp(target, 0, lastIndex)];
            Click(item, ctrl: false, shift, ordered);
            return item;
        }

        /// <summary>
        /// Выделить все строки (Ctrl+A).
        /// </summary>
        public void SelectAll(IReadOnlyList<T> ordered)
        {
            Selected = [.. ordered];
        }

        /// <summary>
        /// Снять выделение (Esc, новое сканирование).
        /// </summary>
        public void Clear()
        {
            Selected = [];
            Anchor = null;
            Cursor = null;
        }

        /// <summary>
        /// Начало рамки выделения. С Ctrl рамка добавляет к уже выделенному, без него — заменяет его.
        /// </summary>
        public void BeginArea(bool additive)
        {
            _areaBase = additive ? [.. Selected] : [];
        }

        /// <summary>
        /// Строки под рамкой изменились.
        /// </summary>
        /// <param name="itemsUnderArea">Строки под рамкой; первая — та, с которой начали тянуть.</param>
        public void UpdateArea(IReadOnlyList<T> itemsUnderArea)
        {
            HashSet<T> next = [.. _areaBase];
            next.UnionWith(itemsUnderArea);
            Selected = next;

            if (itemsUnderArea.Count > 0)
            {
                Anchor = itemsUnderArea[0];
                Cursor = itemsUnderArea[^1];
            }
        }

        /// <summary>
        /// Правый щелчок: строка вне выделения становится единственной выделенной, иначе выделение не трогается.
        /// </summary>
        public void EnsureSelected(T item)
        {
            if (!Selected.Contains(item))
            {
                Selected = [item];
                Anchor = item;
            }

            Cursor = item;
        }

        /// <summary>
        /// Выделение изменили флажки самой сетки.
        /// </summary>
        public void SyncFromGrid(HashSet<T>? items)
        {
            Selected = items ?? [];
        }

        /// <summary>
        /// Выделенные строки в порядке показа; скрытые фильтром — в конце, по ключу.
        /// </summary>
        public List<T> GetSelectedInDisplayOrder(IReadOnlyList<T> ordered)
        {
            List<T> result = [.. ordered.Where(Selected.Contains)];

            if (result.Count < Selected.Count)
            {
                HashSet<T> shown = [.. result];
                result.AddRange(Selected.Where(item => !shown.Contains(item)).OrderBy(_keySelector));
            }

            return result;
        }

        private static HashSet<T> GetRange(T from, T to, IReadOnlyList<T> ordered)
        {
            int fromIndex = IndexOf(ordered, from);
            int toIndex = IndexOf(ordered, to);

            if (fromIndex < 0 || toIndex < 0)
            {
                return [to];
            }

            int start = Math.Min(fromIndex, toIndex);
            int count = Math.Abs(toIndex - fromIndex) + 1;
            return [.. ordered.Skip(start).Take(count)];
        }

        private static int IndexOf(IReadOnlyList<T> items, T item)
        {
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;

            for (int i = 0; i < items.Count; i++)
            {
                if (comparer.Equals(items[i], item))
                {
                    return i;
                }
            }

            return -1;
        }

        #endregion

        #region Grid Binding

        /// <summary>
        /// Строки в порядке показа: после фильтра и сортировки, все страницы.
        /// </summary>
        public IReadOnlyList<T> OrderedItems => _grid?.FilteredItems as IReadOnlyList<T> ?? [.. _grid?.FilteredItems ?? []];

        /// <summary>
        /// Строки текущей страницы.
        /// </summary>
        public IReadOnlyList<T> PageItems
        {
            get
            {
                if (_grid == null)
                {
                    return [];
                }

                return [.. OrderedItems.Skip(_grid.CurrentPage * _grid.RowsPerPage).Take(_grid.RowsPerPage)];
            }
        }

        /// <summary>
        /// Классы строки для <c>RowClassFunc</c>: ключ для JS и подсветка выделения.
        /// </summary>
        public string GetRowClass(T item, int rowIndex)
        {
            string classes = $"fmms-row fmms-row-id-{_keySelector(item)}";

            if (Selected.Contains(item))
            {
                classes += " fmms-row-selected";
            }

            if (Cursor.HasValue && EqualityComparer<T>.Default.Equals(Cursor.Value, item))
            {
                classes += " fmms-row-cursor";
            }

            return classes;
        }

        /// <summary>
        /// Подключить JS-обработчики мыши к обёртке сетки. Вызывать в первом <c>OnAfterRenderAsync</c>.
        /// </summary>
        public async Task AttachAsync(IJSRuntime js, ElementReference container, MudDataGrid<T> grid)
        {
            _grid = grid;
            _selfRef = DotNetObjectReference.Create(this);
            _module = await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
            _handle = await _module.InvokeAsync<IJSObjectReference>("attach", container, _selfRef);
        }

        /// <summary>
        /// Прокрутить к текущей строке после перемещения с клавиатуры. Вызывать в <c>OnAfterRenderAsync</c>.
        /// </summary>
        public async Task AfterRenderAsync()
        {
            if (_handle == null || !_pendingScrollKey.HasValue)
            {
                return;
            }

            int key = _pendingScrollKey.Value;
            _pendingScrollKey = null;
            await _handle.InvokeVoidAsync("scrollRowIntoView", key);
        }

        /// <summary>
        /// Клавиши выделения: Ctrl+A, Esc, стрелки вверх/вниз, Home, End (Ctrl+стрелка — как Home/End).
        /// </summary>
        /// <returns><c>true</c>, если клавиша относилась к выделению.</returns>
        public bool HandleKey(KeyboardEventArgs e)
        {
            if (e.CtrlKey && !e.ShiftKey && e.Code == "KeyA")
            {
                SelectAll(OrderedItems);
                return true;
            }

            if (e.Code == "Escape")
            {
                Clear();
                return true;
            }

            GridNavigation? navigation = e.Code switch
            {
                "ArrowUp" => e.CtrlKey ? GridNavigation.Home : GridNavigation.Up,
                "ArrowDown" => e.CtrlKey ? GridNavigation.End : GridNavigation.Down,
                "Home" => GridNavigation.Home,
                "End" => GridNavigation.End,
                _ => null
            };

            if (!navigation.HasValue)
            {
                return false;
            }

            T? item = Move(navigation.Value, e.ShiftKey, PageItems, OrderedItems);

            if (item.HasValue)
            {
                _pendingScrollKey = _keySelector(item.Value);
            }

            return true;
        }

        #endregion

        #region JS Callbacks

        [JSInvokable]
        public async Task OnRowClick(int key, bool ctrl, bool shift)
        {
            T? item = FindOnPage(key);

            if (item.HasValue)
            {
                Click(item.Value, ctrl, shift, OrderedItems);
                await _onChanged();
            }
        }

        [JSInvokable]
        public void OnAreaStart(bool additive)
        {
            BeginArea(additive);
        }

        [JSInvokable]
        public async Task OnAreaChange(int[] keys)
        {
            Dictionary<int, T> page = PageItems.ToDictionary(_keySelector);
            List<T> items = [.. keys.Where(page.ContainsKey).Select(k => page[k])];

            UpdateArea(items);
            await _onChanged();
        }

        private T? FindOnPage(int key)
        {
            foreach (T item in PageItems)
            {
                if (_keySelector(item) == key)
                {
                    return item;
                }
            }

            return null;
        }

        #endregion

        #region IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_handle != null)
                {
                    await _handle.InvokeVoidAsync("dispose");
                    await _handle.DisposeAsync();
                }

                if (_module != null)
                {
                    await _module.DisposeAsync();
                }
            }
            catch (JSDisconnectedException)
            {
                // WebView уже закрыт вместе со страницей — снимать обработчики не с чего
            }

            _selfRef?.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// Перемещение текущей строки с клавиатуры.
    /// </summary>
    public enum GridNavigation
    {
        Up,
        Down,
        Home,
        End
    }
}
