using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace FastTools
{
    public partial class AddCommandWindow : Window
    {
        private readonly List<StepItem> _steps = new();

        public AddCommandWindow()
        {
            InitializeComponent();
        }

        private void AddStepButton_Click(object sender, RoutedEventArgs e)
        {
            _steps.Add(new StepItem { Type = "command", Value = string.Empty });
            RefreshStepsPanel();
        }

        private void DeleteStepButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int index)
            {
                _steps.RemoveAt(index);
                RefreshStepsPanel();
            }
        }

        private void StepTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.Tag is int index && index >= 0 && index < _steps.Count)
            {
                if (comboBox.SelectedItem is ComboBoxItem item)
                {
                    _steps[index].Type = item.Content.ToString() ?? "command";
                }
            }
        }

        private void RefreshStepsPanel()
        {
            StepsPanel.Items.Clear();
            for (int i = 0; i < _steps.Count; i++)
            {
                var step = _steps[i];
                var stepControl = new StepItemControl
                {
                    Index = i,
                    DeleteRequested = (index) =>
                    {
                        if (index >= 0 && index < _steps.Count)
                        {
                            _steps.RemoveAt(index);
                            RefreshStepsPanel();
                        }
                    },
                    StepChanged = (index, newStep) =>
                    {
                        if (index >= 0 && index < _steps.Count)
                        {
                            _steps[index] = newStep;
                        }
                    }
                };
                stepControl.SetStepData(step);
                StepsPanel.Items.Add(stepControl);
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var alias = AliasTextBox.Text.Trim();
            if (string.IsNullOrEmpty(alias))
            {
                MessageBox.Show("请输入命令别名", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                AliasTextBox.Focus();
                return;
            }

            if (_steps.Count == 0)
            {
                MessageBox.Show("请至少添加一个任务", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            for (int i = 0; i < _steps.Count; i++)
            {
                var step = _steps[i];
                if (string.IsNullOrWhiteSpace(step.Value))
                {
                    MessageBox.Show($"第 {i + 1} 个任务的值不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (step.Type == "delay")
                {
                    if (!int.TryParse(step.Value, out int delayValue) || delayValue <= 0)
                    {
                        MessageBox.Show($"第 {i + 1} 个任务的延迟时间必须是正整数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
            }

            var request = new RequestItem
            {
                Alias = alias,
                Steps = new List<StepItem>(_steps)
            };

            var mainWindow = Owner as MainWindow;
            if (mainWindow != null)
            {
                await mainWindow.AddRequestAsync(request);
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class StepItemControl : UserControl
    {
        private readonly ComboBox _typeComboBox;
        private readonly TextBox _valueTextBox;
        private readonly CheckBox _localDirCheckBox;
        private readonly Button _deleteButton;

        public StepItem Step { get; set; } = new StepItem();
        public int Index { get; set; }
        public Action<int>? DeleteRequested { get; set; }
        public Action<int, StepItem>? StepChanged { get; set; }

        public StepItemControl()
        {
            var border = new Border
            {
                BorderBrush = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new System.Windows.Thickness(1),
                CornerRadius = new System.Windows.CornerRadius(5),
                Padding = new System.Windows.Thickness(10),
                Margin = new System.Windows.Thickness(0, 0, 0, 10)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(120) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = System.Windows.GridLength.Auto });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = System.Windows.GridLength.Auto });

            _typeComboBox = new ComboBox
            {
                Height = 28,
                Margin = new System.Windows.Thickness(0, 0, 10, 0)
            };
            _typeComboBox.Items.Add("command");
            _typeComboBox.Items.Add("adb_command");
            _typeComboBox.Items.Add("delay");
            _typeComboBox.SelectionChanged += TypeComboBox_SelectionChanged;
            System.Windows.Controls.Grid.SetColumn(_typeComboBox, 0);

            _valueTextBox = new TextBox
            {
                Height = 28,
                Padding = new System.Windows.Thickness(5)
            };
            _valueTextBox.TextChanged += ValueTextBox_TextChanged;
            System.Windows.Controls.Grid.SetColumn(_valueTextBox, 1);

            _localDirCheckBox = new CheckBox
            {
                Content = "LocalDir",
                Margin = new System.Windows.Thickness(10, 0, 0, 0),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            _localDirCheckBox.Checked += LocalDirCheckBox_Checked;
            _localDirCheckBox.Unchecked += LocalDirCheckBox_Unchecked;
            System.Windows.Controls.Grid.SetColumn(_localDirCheckBox, 2);

            _deleteButton = new Button
            {
                Width = 28,
                Height = 28,
                Content = "✕",
                Margin = new System.Windows.Thickness(10, 0, 0, 0)
            };
            _deleteButton.Click += DeleteButton_Click;
            System.Windows.Controls.Grid.SetColumn(_deleteButton, 3);

            grid.Children.Add(_typeComboBox);
            grid.Children.Add(_valueTextBox);
            grid.Children.Add(_localDirCheckBox);
            grid.Children.Add(_deleteButton);
            border.Child = grid;
            Content = border;
        }

        public void SetStepData(StepItem step)
        {
            Step = step;
            _valueTextBox.Text = step.Value;
            _localDirCheckBox.IsChecked = step.LocalDir;
            
            foreach (var item in _typeComboBox.Items)
            {
                if (item.ToString() == step.Type)
                {
                    _typeComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void TypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_typeComboBox.SelectedItem != null)
            {
                Step.Type = _typeComboBox.SelectedItem.ToString() ?? "command";
                StepChanged?.Invoke(Index, Step);
            }
        }

        private void ValueTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Step.Value = _valueTextBox.Text;
            StepChanged?.Invoke(Index, Step);
        }

        private void LocalDirCheckBox_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            Step.LocalDir = true;
            StepChanged?.Invoke(Index, Step);
        }

        private void LocalDirCheckBox_Unchecked(object sender, System.Windows.RoutedEventArgs e)
        {
            Step.LocalDir = false;
            StepChanged?.Invoke(Index, Step);
        }

        private void DeleteButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DeleteRequested?.Invoke(Index);
        }
    }
}
