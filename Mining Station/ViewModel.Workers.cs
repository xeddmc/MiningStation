﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Mining_Station
{
    // Workers related methods
    public partial class ViewModel : NotifyObject
    {
        private void WorkerQueryAllCommand(object obj)
        {
            foreach (var w in Workers.WorkerList)
                w.Query = true;
        }

        private void WorkerQueryNoneCommand(object obj)
        {
            foreach (var w in Workers.WorkerList)
                w.Query = false;
        }

        private void CopyWorkerCommand(object obj)
        {
            var itemToCopy = obj as WorkerCopy;
            if (itemToCopy != null)
            {
                var newIndex = Workers.WorkerList.IndexOf(itemToCopy.DestinationWorker);
                if (newIndex != -1)
                    Workers.WorkerListInsert(newIndex, itemToCopy.SourceWorker);
            }
        }

        private void MoveWorkerCommand(object obj)
        {
            var itemToMove = obj as WorkerCopy;
            if (itemToMove != null)
            {
                int oldIndex = 0; int newIndex = 0; int i = 0;
                foreach (var w in Workers.WorkerList)
                {
                    if (w == itemToMove.SourceWorker)
                        oldIndex = i;
                    if (w == itemToMove.DestinationWorker)
                        newIndex = i;
                    i++;
                }
                if (oldIndex != newIndex)
                    Workers.WorkerListMove(oldIndex, newIndex);
            }
        }

        private void CopyCoinTableCommand(object obj)
        {
            var itemToCopy = obj as CoinTableCopy;
            if (itemToCopy == null)
                return;
            var destinationWorkerIndex = Workers.WorkerList.IndexOf(itemToCopy.DestinationWorker);
            Workers.WorkerList[destinationWorkerIndex].CoinTableInsert(itemToCopy.DestinationCoinTableIndex, itemToCopy.SourceCoinTable.Clone());
        }

        private void MoveCoinTableCommand(object obj)
        {
            var itemToCopy = obj as CoinTableCopy;
            if (itemToCopy == null)
                return;
            var destinationWorkerIndex = Workers.WorkerList.IndexOf(itemToCopy.DestinationWorker);
            var sourceWorkerIndex = Workers.WorkerList.IndexOf(itemToCopy.SourceWorker);
            if (destinationWorkerIndex == sourceWorkerIndex) // Move within the same worker
            {
                if (itemToCopy.SourceCoinTableIndex == itemToCopy.DestinationCoinTableIndex)
                    return;
                else Workers.WorkerList[sourceWorkerIndex].CoinTableMove(itemToCopy.SourceCoinTableIndex, itemToCopy.DestinationCoinTableIndex);
            }
            else
            {
                Workers.WorkerList[sourceWorkerIndex].CoinTableRemoveAt(itemToCopy.SourceCoinTableIndex);
                Workers.WorkerList[destinationWorkerIndex].CoinTableInsert(itemToCopy.DestinationCoinTableIndex, itemToCopy.SourceCoinTable.Clone());
            }
        }

        private void WorkersExpandAllCommand(object obj)
        {
            Helpers.MouseCursorWait();
            bool someChanged = false;
            foreach (var w in Workers.WorkerList)
            {
                if (!w.IsExpanded)
                    someChanged = true;
                w.IsExpanded = true;
            }
            if (!someChanged)
                Helpers.MouseCursorNormal();
        }

        private void WorkersCollapseAllCommand(object obj)
        {
            foreach (var w in Workers.WorkerList)
                w.IsExpanded = false;
        }

        private void WorkerSelectNoneCommand(object obj)
        {
            Debug.WriteLine("Select None");
            var w = (Worker)obj;
            if (w != null)
            {
                foreach (var row in w.CoinList)
                    row.Switch = false;
            }
        }

        private void WorkerSelectAllCommand(object obj)
        {
            Debug.WriteLine("Select All");
            var w = (Worker)obj;
            if (w != null)
            {
                foreach (var row in w.CoinList)
                    row.Switch = true;
            }
        }

        private void AddWorkerCommand(object parameter)
        {
            var workerIndex = Workers.WorkerList.IndexOf((Worker)parameter);
            Workers.WorkerIndex = workerIndex;
            if (workerIndex != -1)
            {
                var newWorker = Workers.WorkerList[workerIndex].Clone();
                Workers.WorkerListInsert(workerIndex, newWorker);
            }
        }

        private void DeleteWorkerCommand(object parameter)
        {
            var workerIndex = Workers.WorkerList.IndexOf((Worker)parameter);
            Workers.WorkerIndex = workerIndex;
            if (workerIndex != -1)
            {
                var name = Workers.WorkerList[workerIndex].Name;
                if (name == string.Empty)
                    name = "unnamed worker";
                var response = MessageBox.Show("Delete " + name + "?", "Delete worker", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (response == MessageBoxResult.Yes)
                    Workers.WorkerListRemoveAt(workerIndex);
            }
        }

        private void NewWorkerCommand(object parameter)
        {
            var workerIndex = Workers.WorkerList.IndexOf((Worker)parameter);
            Workers.WorkerIndex = workerIndex;
            if (workerIndex != -1)
            {
                var index = workerIndex + 1;
                if (index < Workers.WorkerList.Count)
                    Workers.WorkerListInsert(index, Worker.DefaultWorker());
                else Workers.WorkerListAdd(Worker.DefaultWorker());
            }
        }

        private void MultiplyHashrateCommand(object parameter)
        {
            var ct = parameter as CoinTable;
            if (ct == null)
                return;
            var window = new HashrateMultiplier();
            var vm = new HashrateMultiplierVM(ct.Coins, Workers.DisplayCoinAs);
            window.DataContext = vm;
            var dialogresult = window.ShowDialog();
            if (dialogresult == false)
                return;
            SaveUndoRedo("WorkersAll");
            SaveUndoIsEnabled = false;
            foreach (var worker in Workers.WorkerList)
            {
                foreach (var coinTable in worker.CoinList)
                {
                    foreach (var coin in coinTable.Coins)
                    {
                        foreach (var coinPlus in vm.Coins)
                        {
                            if (coinPlus.Coin.Name != coin.Name || coinPlus.Multiplier == 1)
                                continue;
                            switch (coinPlus.Operation)
                            {
                                case "*":
                                    coin.Hashrate *= coinPlus.Multiplier;
                                    break;
                                case "/":
                                    coin.Hashrate /= coinPlus.Multiplier;
                                    break;
                            }
                            var i = HashrateMultiplierVM.RoundConverter(coinPlus.Rounding);
                            if (i >= 0)
                                coin.Hashrate = Math.Round(coin.Hashrate, i);
                        }
                    }
                }
            }
            SaveUndoIsEnabled = true;
        }

        public bool[] SaveQueries()
        {
            //Store current Query data and reset Query checkmarks
            bool[] queries = new bool[Workers.WorkerList.Count];
            for (int i = 0; i < Workers.WorkerList.Count; i++)
            {
                queries[i] = Workers.WorkerList[i].Query;
                Workers.WorkerList[i].Query = false;
            }
            return queries;
        }

        public void RestoreQueries(bool[] queries)
        {
            for (int i = 0; i < queries.Length; i++)
                Workers.WorkerList[i].Query = queries[i];
        }

        private async void ExportWorkersCommand(object parameter)
        {
            var window = new SelectWorkers();
            window.SizeToContent = SizeToContent.WidthAndHeight;
            var vm = new SelectWorkersVM();
            vm.Workers = Workers.WorkerList;

            bool[] queries = SaveQueries();

            window.DataContext = vm;
            vm.Title = "Export Workers";
            vm.ButtonTitle = "Export";
            var dialogResult = window.ShowDialog();
            if (dialogResult == false)
            {
                RestoreQueries(queries);
                return;
            }

            var workersToSave = Workers.WorkerList.Where(x => x.Query).ToList();
            if (workersToSave == null || workersToSave.Count == 0)
            {
                RestoreQueries(queries);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "JSON file|*.json";
            saveFileDialog.OverwritePrompt = true;
            var saveDialogResult = saveFileDialog.ShowDialog();
            if (saveDialogResult == false)
            {
                RestoreQueries(queries);
                return;
            }

            var json = JsonConverter.ConvertToJson(workersToSave);
            if (json == null)
            {
                await Task.Delay(100);
                MessageBox.Show("Failed to convert selected workers to JSON format", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                RestoreQueries(queries);
                return;
            }
            string jsonFormatted = JsonConverter.FormatJson(json);
            Helpers.WriteToTxtFile(saveFileDialog.FileName, jsonFormatted);
            RestoreQueries(queries);
        }

        private async void ImportWorkersCommand(object parameter)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "JSON file|*.json";
            var openFileDialogResult = openFileDialog.ShowDialog();
            if (openFileDialogResult == false || string.IsNullOrEmpty(openFileDialog.FileName))
                return;

            string workersContent = null;
            ObservableCollection<Worker> convertedWorkers = null;
            try { workersContent = System.IO.File.ReadAllText(openFileDialog.FileName); }
            catch
            {
                await Task.Delay(100);
                MessageBox.Show($"There was an error while reading from \"{openFileDialog.FileName}\"", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            convertedWorkers = JsonConverter.ConvertFromJson<ObservableCollection<Worker>>(workersContent, false);
            if (convertedWorkers == null || convertedWorkers.Count == 0)
            {
                await Task.Delay(100);
                Helpers.ShowErrorMessage($"Failed to intepret JSON information from \"{openFileDialog.FileName}\"", "Error");
                return;
            }

            var window = new SelectWorkers();
            window.SizeToContent = SizeToContent.WidthAndHeight;
            var vm = new SelectWorkersVM();
            vm.Workers = convertedWorkers;
            foreach (var worker in vm.Workers)
                worker.Query = true;
            window.DataContext = vm;
            vm.Title = "Import Workers";
            vm.ButtonTitle = "Import";
            var dialogResult = window.ShowDialog();
            if (dialogResult == false)
            {
                return;
            }

            var selectedWorkers = vm.Workers.Where(x => x.Query).ToList();
            if (selectedWorkers == null || selectedWorkers.Count == 0)
                return;

            var workerIndex = Workers.WorkerList.IndexOf((Worker)parameter);
            Workers.WorkerListAddRangeAt(selectedWorkers, workerIndex);
        }

        private void AddCoinTableCommand(object parameter)
        {
            Debug.WriteLine("+ Add coin table clicked");
            var workerIndex = Workers.WorkerList.IndexOf((Worker)((object[])parameter)[0]);
            CoinTableIndex = Workers.WorkerList[workerIndex].CoinList.IndexOf((CoinTable)((object[])parameter)[1]);
            if (workerIndex != -1 && CoinTableIndex != -1)
            {
                var newCoinTable = Workers.WorkerList[workerIndex].CoinList[CoinTableIndex].Clone();
                Workers.WorkerList[workerIndex].CoinTableInsert(CoinTableIndex + 1, newCoinTable);
            }
        }

        private void DeleteCoinTableCommand(object parameter)
        {
            var workerIndex = Workers.WorkerList.IndexOf((Worker)((object[])parameter)[0]);
            CoinTableIndex = Workers.WorkerList[workerIndex].CoinList.IndexOf((CoinTable)((object[])parameter)[1]);
            if (workerIndex != -1 && CoinTableIndex != -1)
            {
                string showName = string.Empty;
                var fullName = Workers.WorkerList[workerIndex].CoinList[CoinTableIndex].FullName;
                var fullSymbol = Workers.WorkerList[workerIndex].CoinList[CoinTableIndex].FullSymbol;
                if (fullName == string.Empty || fullSymbol == string.Empty)
                    showName = "coin record";
                else showName = fullName + " (" + fullSymbol + ")";
                var response = MessageBox.Show("Delete " + showName + "?", "Delete row", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (response == MessageBoxResult.Yes)
                    Workers.WorkerList[workerIndex].CoinTableRemoveAt(CoinTableIndex);
            }
        }

        private void MoveWorkerUpCommand(object parameter)
        {
            var workerIndex = Workers.WorkerList.IndexOf((Worker)parameter);
            if (workerIndex != -1)
            {
                Workers.WorkerListMove(workerIndex, workerIndex - 1);
            }
        }

        private void MoveWorkerDownCommand(object parameter)
        {
            var workerIndex = Workers.WorkerList.IndexOf((Worker)parameter);
            if (workerIndex != -1)
            {
                Workers.WorkerListMove(workerIndex, workerIndex + 1);
            }
        }

        private void AddCoinCommand(object parameter)
        {
            Debug.WriteLine("ADD Coin: " + parameter);
            var workerIndex = Workers.WorkerList.IndexOf((Worker)((object[])parameter)[0]);
            CoinTableIndex = Workers.WorkerList[workerIndex].CoinList.IndexOf((CoinTable)((object[])parameter)[1]);
            CoinIndex = Workers.WorkerList[workerIndex].CoinList[CoinTableIndex].Coins.IndexOf((Coin)((object[])parameter)[2]);
            Debug.WriteLine("ADD Coin to Worker: " + workerIndex + " of " + Workers.WorkerList.Count + " CoinTableIndex: " + CoinTableIndex + " of " + Workers.WorkerList[workerIndex].CoinList.Count + " CoinIndex: " + CoinIndex + " of " + Workers.WorkerList[workerIndex].CoinList[CoinTableIndex].Coins.Count);
            if (workerIndex != -1 && CoinTableIndex != -1 && CoinIndex != -1)
            {
                var newCoin = Workers.WorkerList[workerIndex].CoinList[CoinTableIndex].Coins[CoinIndex].Clone();
                Workers.WorkerList[workerIndex].CoinList[CoinTableIndex].CoinInsert(CoinIndex + 1, newCoin);
            }
        }

        private void DeleteCoinCommand(object parameter)
        {
            var workerIndex = Workers.WorkerList.IndexOf((Worker)((object[])parameter)[0]);
            CoinTableIndex = Workers.WorkerList[workerIndex].CoinList.IndexOf((CoinTable)((object[])parameter)[1]);
            CoinIndex = Workers.WorkerList[workerIndex].CoinList[CoinTableIndex].Coins.IndexOf((Coin)((object[])parameter)[2]);
            if (workerIndex != -1 && CoinTableIndex != -1 && CoinIndex != -1)
            {
                Workers.WorkerList[workerIndex].CoinList[CoinTableIndex].CoinRemoveAt(CoinIndex);
            }
        }

        private void AddComputersCommand(object obj)
        {
            var addComputersWindow = new AddComputers();
            var vm = new AddComputersVM();
            addComputersWindow.DataContext = vm;
            var dialogResult = addComputersWindow.ShowDialog();
            if (dialogResult == true)
            {
                var worker = obj as Worker;
                if (worker == null)
                    return;
                worker.RaiseProperychanging("Computers");
                foreach (dynamic pc in vm.Computers)
                {
                    if (pc.IsSelected)
                        worker.Computers.Add(pc.Name);
                }
                worker.RaiseProperychanged("Computers");
            }
            vm.Dispose();
        }

        private void EditPathCommand(object obj)
        {
            (object CoinTable, string Path) t;
            try
            {
                t = ((object CoinTable, string Path))obj;
            }
            catch
            {
                return;
            }
            var coinTable = t.CoinTable as CoinTable;
            if (coinTable == null)
                return;
            var path = t.Path as string;
            if (path == null)
                return;
            coinTable.Path = path;
        }

        private void OpenInExplorerCommand(object obj)
        {
            var t = obj as CoinTable;
            if (t == null)
                return;
            var path = t.Path;
            if (!File.Exists(path))
                MessageBox.Show($"File not found.\n{path}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            else
            {
                string argument = "/select, \"" + path + "\"";
                Process.Start("explorer.exe", argument);
            }
        }

        private void SortByCommand(object obj)
        {
            var t = obj as Tuple<object, object, string>;
            if (t == null)
                return;
            var column = t.Item1 as string;
            var worker = t.Item2 as Worker;
            var secondSortBy = t.Item3;
            if (column == null || worker == null)
                return;

            Func<string, string> mapProperty = (string key) => {
                switch (key)
                {
                    case "Coin": return "FullName";
                    case "Coin Name": return "FullName";
                    case "Coin Symbol": return "FullSymbol";
                    case "Hashrate": return "FullHashrate";
                    case "Algorithm": return "FullAlgorithm";
                    default: return key;
                }
            };

            var propertyName = mapProperty(column);
            var secondaryPropertyName = mapProperty(secondSortBy);
            var newCoinList = new ObservableCollection<CoinTable>();
            IOrderedEnumerable<CoinTable> sorted = null;
            if (secondaryPropertyName != null)
            {
                sorted = worker.CoinList.OrderBy(x => x.GetType().GetProperty(propertyName).GetValue(x, null)).ThenBy(y => y.GetType().GetProperty(secondaryPropertyName).GetValue(y, null));
            }
            else
            {
                sorted = worker.CoinList.OrderBy(x => x.GetType().GetProperty(propertyName).GetValue(x, null));
            }
            foreach (var item in sorted)
                newCoinList.Add(item.Clone());
            worker.RaiseProperychanging("CoinList");
            worker.CoinList = newCoinList;
            worker.RaiseProperychanged("CoinList");
        }
    }
}
