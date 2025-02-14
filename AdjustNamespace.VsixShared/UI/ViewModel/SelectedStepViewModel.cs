﻿using AdjustNamespace.Helper;
using EnvDTE80;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using AdjustNamespace.UI.StepFactory;
using Microsoft.CodeAnalysis;

namespace AdjustNamespace.UI.ViewModel
{
    public class SelectedStepViewModel : ChainViewModel
    {
        private readonly IAsyncServiceProvider _serviceProvider;
        private readonly IStepFactory _nextStepFactory;
        private readonly List<string> _filePaths;

        private Brush _foreground;
        private string _mainMessage;
        private bool _isInProgress = false;

        private ICommand? _closeCommand;
        private ICommand? _nextCommand;
        private ICommand? _invertStatusCommand;

        public string MainMessage
        {
            get => _mainMessage;
            private set
            {
                _mainMessage = value;
                OnPropertyChanged(nameof(MainMessage));
            }
        }

        public ObservableCollection<SelectItemViewModel> ToFilterItems
        {
            get;
            private set;
        }

        public Brush Foreground
        {
            get => _foreground;
            private set
            {
                _foreground = value;
                OnPropertyChanged(nameof(Foreground));
            }
        }


        public ICommand CloseCommand
        {
            get
            {
                if (_closeCommand == null)
                {
                    _closeCommand = new RelayCommand(
                        a =>
                        {
                            if (a is DialogWindow w)
                            {
                                w.Close();
                            }
                        },
                        r => !_isInProgress
                        );
                }

                return _closeCommand;
            }
        }

        public ICommand NextCommand
        {
            get
            {
                if (_nextCommand == null)
                {
                    _nextCommand = new AsyncRelayCommand(
                        async a => await _nextStepFactory.CreateAsync(
                            ToFilterItems.Where(s => s.IsChecked).Select(s => s.FilePath).ToList()
                            ),
                        r => !_isInProgress && ToFilterItems.Any(s => s.IsChecked)
                        );
                }

                return _nextCommand;
            }
        }


        public ICommand InvertStatusCommand
        {
            get
            {
                if (_invertStatusCommand == null)
                {
                    _invertStatusCommand = new RelayCommand(
                        a =>
                        {
                            var selected = ToFilterItems.Where(i => i.IsSelected).ToList();

                            if (selected.Count == 0)
                            {
                                return;
                            }

                            var newValue = !selected[0].IsChecked;
                            selected.ForEach(s => s.IsChecked = newValue);
                        }
                        );
                }

                return _invertStatusCommand;
            }
        }


        public SelectedStepViewModel(
            IAsyncServiceProvider serviceProvider,
            IStepFactory nextStepFactory,
            List<string> filePaths
            )
        {
            if (serviceProvider is null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (nextStepFactory is null)
            {
                throw new ArgumentNullException(nameof(nextStepFactory));
            }

            if (filePaths is null)
            {
                throw new ArgumentNullException(nameof(filePaths));
            }
            _serviceProvider = serviceProvider;
            _nextStepFactory = nextStepFactory;
            _filePaths = filePaths;

            _foreground = Brushes.Green;
            _mainMessage = "Choose files to process...";
            ToFilterItems = new ObservableCollection<SelectItemViewModel>();
        }

        public override async System.Threading.Tasks.Task StartAsync()
        {
            _isInProgress = true;
            OnPropertyChanged();

            var dte = await _serviceProvider.GetServiceAsync(typeof(EnvDTE.DTE)) as DTE2;
            if (dte == null)
            {
                return;
            }

            var componentModel = (await _serviceProvider.GetServiceAsync(typeof(SComponentModel)) as IComponentModel)!;
            if (componentModel == null)
            {
                return;
            }

            var workspace = componentModel.GetService<VisualStudioWorkspace>();
            if (workspace == null)
            {
                return;
            }

            #region check for solution compilation

            for (var i = 0; i < _filePaths.Count; i++)
            {
                var subjectFilePath = _filePaths[i];

                if (subjectFilePath.EndsWith(".xaml"))
                {
                    ToFilterItems.Add(
                       new SelectItemViewModel(
                            subjectFilePath
                        )
                    );

                    continue;
                }

                if (!dte.Solution.TryGetProjectItem(subjectFilePath, out var subjectProject, out var subjectProjectItem))
                {
                    continue;
                }

                var roslynProject = workspace.CurrentSolution.Projects.FirstOrDefault(p => p.FilePath == subjectProject!.FullName);
                if (roslynProject == null)
                {
                    continue;
                }

                var subjectDocument = workspace.GetDocument(subjectFilePath);
                if (subjectDocument == null)
                {
                    continue;
                }

                var subjectSyntaxRoot = await subjectDocument.GetSyntaxRootAsync();
                if (subjectSyntaxRoot == null)
                {
                    continue;
                }

                if (!roslynProject.TryGetTargetNamespace(subjectFilePath, out var targetNamespace))
                {
                    continue;
                }

                var namespaceInfos = subjectSyntaxRoot.GetAllNamespaceInfos(targetNamespace!);
                if (namespaceInfos.Count == 0)
                {
                    continue;
                }

                ToFilterItems.Add(
                    new SelectItemViewModel(
                        subjectFilePath
                    )
                );
            }

            #endregion

            _isInProgress = false;
            OnPropertyChanged();
        }
    }

    public class SelectItemViewModel : BaseViewModel
    {
        private bool _isChecked;
        private bool _isSelected;

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                _isChecked = value;
                OnPropertyChanged(nameof(IsChecked));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => _isSelected = value;
        }

        public string FilePath
        {
            get;
        }

        public SelectItemViewModel(string filePath)
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            FilePath = filePath;
            IsChecked = true;
        }
    }
}
