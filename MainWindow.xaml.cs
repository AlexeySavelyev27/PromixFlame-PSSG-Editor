using Microsoft.Win32;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace PSSGEditor
{
    public partial class MainWindow : Window
    {
        private PSSGNode rootNode;
        private PSSGNode currentNode;
        private int rawDataOriginalLength = 0;
        private bool isLoadingRawData = false;

        // Чтобы сохранить вертикальный offset ScrollViewer до редактирования
        private double savedVerticalOffset = 0;

        // Для запоминания сортировки
        private string savedSortMember = null;
        private ListSortDirection? savedSortDirection = null;

        // Свойство для доступа из partial class 3DViewer
        public PSSGNode RootNode => rootNode;

        public MainWindow()
        {
            InitializeComponent();

            // Окончание редактирования – сохраняем данные
            AttributesDataGrid.CellEditEnding += AttributesDataGrid_CellEditEnding;

            // Запоминаем новые параметры сортировки
            AttributesDataGrid.Sorting += AttributesDataGrid_Sorting;

            // Обработчик PreparingCellForEdit привязан в XAML
            
            // Инициализация 3D вьювера
            Initialize3DViewer();
        }
        
        /// <summary>
        /// Обработчик выбора элемента в дереве 3D объектов
        /// </summary>
        private void Objects3DTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem item)
            {
                On3DObjectSelected(item);
            }
        }
    }
}

