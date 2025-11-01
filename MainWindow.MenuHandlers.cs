using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Threading.Tasks;

namespace PSSGEditor
{
    public partial class MainWindow
    {
        private async void OpenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "PSSG/.ens files (*.pssg;*.ens)|*.pssg;*.ens",
                Title = "Open PSSG File"
            };
            if (ofd.ShowDialog() != true) return;

            await LoadFileAsync(ofd.FileName);
        }

        public async Task LoadFileAsync(string fileName)
        {
            StatusText.Text = "Loading...";
            LoadingProgressBar.Visibility = Visibility.Visible;
            LoadingProgressBar.IsIndeterminate = true;
            
            try
            {
                var node = await Task.Run(() =>
                {
                    var parser = new PSSGParser(fileName);
                    return parser.Parse();
                });

                rootNode = node;

                var stats = CollectStats(rootNode);
                StatusText.Text = $"Nodes: {stats.nodes}, Meshes: {stats.meshes}, Textures: {stats.textures}";

                PopulateTreeView();
                PopulateTextureList();
                Build3DObjectsTree();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error opening file";
            }
            finally
            {
                LoadingProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void SaveAsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (rootNode == null) return;

            var sfd = new SaveFileDialog
            {
                Filter = "PSSG files (*.pssg)|*.pssg",
                Title = "Save as PSSG"
            };
            if (sfd.ShowDialog() != true) return;

            try
            {
                var writer = new PSSGWriter(rootNode);
                writer.Save(sfd.FileName);
                StatusText.Text = $"Saved: {sfd.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error saving file";
            }
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ExportJsonMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (rootNode == null)
            {
                MessageBox.Show("No file loaded. Please open a PSSG file first.", "No File", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                Title = "Export Node Tree to JSON",
                FileName = "pssg_structure.json"
            };

            if (sfd.ShowDialog() != true) return;

            try
            {
                StatusText.Text = "Exporting to JSON...";
                
                // Create exporter with default settings
                var exporter = new JsonExporter(
                    includeRawDataPreview: true,
                    maxRawDataPreviewBytes: 128,
                    maxDepth: -1 // No depth limit
                );

                exporter.ExportToFile(rootNode, sfd.FileName);
                
                StatusText.Text = $"Exported to: {sfd.FileName}";
                MessageBox.Show($"Successfully exported node tree to:\n{sfd.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export to JSON:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error exporting to JSON";
            }
        }

        private void ExportSelectedNodeJsonMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (rootNode == null)
            {
                MessageBox.Show("No file loaded. Please open a PSSG file first.", "No File", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedNode = PssgTreeView.SelectedItem as PSSGNode;
            
            if (selectedNode == null)
            {
                MessageBox.Show("Please select a node in the tree view first.", "No Node Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                Title = "Export Selected Node to JSON",
                FileName = $"{selectedNode.Name}.json"
            };

            if (sfd.ShowDialog() != true) return;

            try
            {
                StatusText.Text = "Exporting selected node to JSON...";
                
                var exporter = new JsonExporter(
                    includeRawDataPreview: true,
                    maxRawDataPreviewBytes: 256,
                    maxDepth: -1
                );

                exporter.ExportToFile(selectedNode, sfd.FileName);
                
                StatusText.Text = $"Exported selected node to: {sfd.FileName}";
                MessageBox.Show($"Successfully exported node '{selectedNode.Name}' to:\n{sfd.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export to JSON:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error exporting to JSON";
            }
        }

        private void DeleteNodeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (rootNode == null)
            {
                MessageBox.Show("No file loaded.", "No File", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedNode = PssgTreeView.SelectedItem as PSSGNode;

            if (selectedNode == null)
            {
                MessageBox.Show("Please select a node to delete.", "No Node Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Нельзя удалить корневую ноду
            if (selectedNode == rootNode)
            {
                MessageBox.Show("Cannot delete root node.", "Invalid Operation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Подтверждение удаления
            var result = MessageBox.Show(
                $"Are you sure you want to delete node '{selectedNode.Name}' and all its children?",
                "Confirm Deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                // Найти родителя и удалить ноду
                if (FindAndDeleteNode(rootNode, selectedNode))
                {
                    // Обновить отображение
                    PopulateTreeView();
                    PopulateTextureList();

                    var stats = CollectStats(rootNode);
                    StatusText.Text = $"Node deleted. Nodes: {stats.nodes}, Meshes: {stats.meshes}, Textures: {stats.textures}";
                }
                else
                {
                    MessageBox.Show("Failed to delete node: parent not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting node:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool FindAndDeleteNode(PSSGNode parent, PSSGNode nodeToDelete)
        {
            // Проверить непосредственных детей
            if (parent.Children.Remove(nodeToDelete))
                return true;

            // Рекурсивный поиск в поддереве
            foreach (var child in parent.Children)
            {
                if (FindAndDeleteNode(child, nodeToDelete))
                    return true;
            }

            return false;
        }
    }
}
