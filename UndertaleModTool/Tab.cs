﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using UndertaleModLib.Models;
using UndertaleModLib;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using static UndertaleModLib.Models.UndertaleRoom;

namespace UndertaleModTool
{
    /// <summary>
    /// Stores various information about a tab in the object editor.
    /// </summary>
    public class Tab : INotifyPropertyChanged
    {
        /// <summary>The default icon for the close button.</summary>
        public static readonly BitmapImage ClosedIcon = new(new Uri(@"/Resources/X.png", UriKind.RelativeOrAbsolute));

        /// <summary>The icon for the hovered close button.</summary>
        public static readonly BitmapImage ClosedHoverIcon = new(new Uri(@"/Resources/X_Down.png", UriKind.RelativeOrAbsolute));

        private static readonly MainWindow mainWindow = Application.Current.MainWindow as MainWindow;

        /// <inheritdoc/>
        public event PropertyChangedEventHandler PropertyChanged;

        private object _currentObject;

        /// <summary>The currently opened object in this tab.</summary>
        [PropertyChanged.DoNotNotify] // Prevents "PropertyChanged.Invoke()" injection on compile
        public object CurrentObject
        {
            get => _currentObject;
            set
            {
                object prevObj = _currentObject;
                _currentObject = value;

                SetTabTitleBinding(value, prevObj);

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentObject)));
                mainWindow.RaiseOnSelectedChanged();
            }
        }

        /// <summary>The tab title.</summary>
        /// <value>"Untitled" by default.</value>
        public string TabTitle { get; set; } = "Untitled";

        /// <summary>Whether the title of this tab is autogenerated.</summary>
        public bool IsCustomTitle { get; set; }

        /// <summary>The index of this tab.</summary>
        public int TabIndex { get; set; }

        /// <summary>Whether this tab should be closed automatically.</summary>
        public bool AutoClose { get; set; } = false;


        /// <summary>The history of objects opened in this tab.</summary>
        public ObservableCollection<object> History { get; } = new();

        /// <summary>The current position in the opened object history.</summary>
        public int HistoryPosition { get; set; }

        /// <summary>The last state of the opened editor.</summary>
        public TabContentState LastContentState { get; set; }

        /// <summary>Initializes a new instance of <see cref="Tab"/>.</summary>
        /// <param name="obj">The object that should be open.</param>
        /// <param name="tabIndex">The tab index.</param>
        /// <param name="tabTitle">The tab title.</param>
        public Tab(object obj, int tabIndex, string tabTitle = null)
        {
            CurrentObject = obj;
            TabIndex = tabIndex;
            AutoClose = obj is DescriptionView;

            IsCustomTitle = tabTitle is not null;
            if (IsCustomTitle)
            {
                if (tabTitle.Length > 64)
                    TabTitle = tabTitle[..64] + "...";
                else
                    TabTitle = tabTitle;
            }
        }

        /// <summary>Generates a tab title depending on a type of the object.</summary>
        public static string GetTitleForObject(object obj)
        {
            if (obj is null)
                return null;

            string title = null;

            if (obj is DescriptionView view)
            {
                if (view.Heading.Contains("Welcome"))
                {
                    title = "Welcome!";
                }
                else
                {
                    title = view.Heading;
                }
            }
            else if (obj is UndertaleNamedResource namedRes)
            {
                string content = namedRes.Name?.Content;

                string header = obj switch
                {
                    UndertaleAudioGroup => "Audio Group",
                    UndertaleSound => "Sound",
                    UndertaleSprite => "Sprite",
                    UndertaleBackground => "Background",
                    UndertalePath => "Path",
                    UndertaleScript => "Script",
                    UndertaleShader => "Shader",
                    UndertaleFont => "Font",
                    UndertaleTimeline => "Timeline",
                    UndertaleGameObject => "Game Object",
                    UndertaleRoom => "Room",
                    UndertaleExtension => "Extension",
                    UndertaleTexturePageItem => "Texture Page Item",
                    UndertaleCode => "Code",
                    UndertaleVariable => "Variable",
                    UndertaleFunction => "Function",
                    UndertaleCodeLocals => "Code Locals",
                    UndertaleEmbeddedTexture => "Embedded Texture",
                    UndertaleEmbeddedAudio => "Embedded Audio",
                    UndertaleTextureGroupInfo => "Texture Group Info",
                    UndertaleEmbeddedImage => "Embedded Image",
                    UndertaleSequence => "Sequence",
                    UndertaleAnimationCurve => "Animation Curve",
                    _ => null
                };

                if (header is not null)
                    title = header + " - " + content;
                else
                    Debug.WriteLine($"Could not handle type {obj.GetType()}");
            }
            else if (obj is UndertaleString str)
            {
                string stringFirstLine = str.Content;
                if (stringFirstLine is not null)
                {
                    if (stringFirstLine.Length == 0)
                        stringFirstLine = "(empty string)";
                    else
                    {
                        int stringLength = StringTitleConverter.NewLineRegex.Match(stringFirstLine).Index;
                        if (stringLength != 0)
                            stringFirstLine = stringFirstLine[..stringLength] + " ...";
                    }
                }

                title = "String - " + stringFirstLine;
            }
            else if (obj is UndertaleChunkVARI)
            {
                title = "Variables Overview";
            }
            else if (obj is GeneralInfoEditor)
            {
                title = "General Info";
            }
            else if (obj is GlobalInitEditor)
            {
                title = "Global Init";
            }
            else if (obj is GameEndEditor)
            {
                title = "Game End";
            }
            else
            {
                Debug.WriteLine($"Could not handle type {obj.GetType()}");
            }

            if (title is not null)
            {
                // "\t" is displayed as 8 spaces.
                // So, replace all "\t" with spaces,
                // in order to properly shorten the title.
                title = title.Replace("\t", "        ");

                if (title.Length > 64)
                    title = title[..64] + "...";
            }

            return title;
        }

        /// <summary>Changes a data binding of the title.</summary>
        /// <param name="obj">The current object.</param>
        /// <param name="prevObj">The previous object.</param>
        /// <param name="textBlock">A reference to the <see cref="TextBlock"/> that displays the title.</param>
        public static void SetTabTitleBinding(object obj, object prevObj, TextBlock textBlock = null)
        {
            if (textBlock is null)
            {
                var cont = mainWindow.TabController.ItemContainerGenerator.ContainerFromIndex(mainWindow.CurrentTabIndex);
                textBlock = MainWindow.FindVisualChild<TextBlock>(cont);
            }
            else
                obj = (textBlock.DataContext as Tab)?.CurrentObject;

            if (obj is null || textBlock is null)
                return;

            bool objNamed = obj is UndertaleNamedResource;
            bool objString = obj is UndertaleString;

            if (prevObj is not null)
            {
                bool pObjNamed = prevObj is UndertaleNamedResource;
                bool pObjString = prevObj is UndertaleString;

                // If both objects have the same type (one of above)
                // or both objects are not "UndertaleNamedResource",
                // then there's no need to change the binding
                if (pObjNamed && objNamed || pObjString && objString || !(pObjNamed || objNamed))
                    return;
            }

            MultiBinding binding = new()
            {
                Converter = TabTitleConverter.Instance,
                Mode = BindingMode.OneWay
            };
            binding.Bindings.Add(new Binding() { Mode = BindingMode.OneTime });

            // These bindings are only for notification
            binding.Bindings.Add(new Binding("CurrentObject") { Mode = BindingMode.OneWay });
            if (objNamed)
                binding.Bindings.Add(new Binding("CurrentObject.Name.Content") { Mode = BindingMode.OneWay });
            else if (objString)
                binding.Bindings.Add(new Binding("CurrentObject.Content") { Mode = BindingMode.OneWay });

            textBlock.SetBinding(TextBlock.TextProperty, binding);
        }

        /// <summary>Saves the current tab content state.</summary>
        /// <param name="dataEditor">A reference to the object editor of main window.</param>
        public void SaveTabContentState(ContentControl dataEditor)
        {
            if (dataEditor is null
                || dataEditor.Content is null
                || dataEditor.Content is DescriptionView)
                return;

            UserControl editor;
            try
            {
                var contPres = VisualTreeHelper.GetChild(dataEditor, 0);
                editor = (UserControl)VisualTreeHelper.GetChild(contPres, 0);
            }
            catch
            {
                mainWindow.ShowWarning("The last tab content state can't be saved - \"UserControl\" is not found.");
                return;
            }

            double mainScrollPos = MainWindow.GetNearestParent<ScrollViewer>(dataEditor)?.VerticalOffset ?? 0;

            switch (editor)
            {
                case UndertaleCodeEditor codeEditor:
                    #pragma warning disable CA1416
                    bool isDecompiledOpen = codeEditor.CodeModeTabs.SelectedIndex == 0;
                    
                    var textEditor = codeEditor.DecompiledEditor;
                    (int, int) decompCodePos;
                    int linePos, columnPos;
                    // If the overridden position wasn't read
                    if (UndertaleCodeEditor.OverriddenDecompPos != default)
                    {
                        decompCodePos = UndertaleCodeEditor.OverriddenDecompPos;
                        UndertaleCodeEditor.OverriddenDecompPos = default;
                    }
                    else
                    {
                        var caret = textEditor.TextArea.Caret;
                        linePos = caret.Line;
                        columnPos = caret.Column;

                        int lineLen = textEditor.Document.GetLineByNumber(linePos).Length;
                        // If caret is at the end of line
                        if (lineLen == columnPos - 1)
                            columnPos = -1;

                        decompCodePos = (linePos, columnPos);
                    }

                    textEditor = codeEditor.DisassemblyEditor;
                    (int, int) disasmCodePos;
                    if (UndertaleCodeEditor.OverriddenDisasmPos != default)
                    {
                        disasmCodePos = UndertaleCodeEditor.OverriddenDisasmPos;
                        UndertaleCodeEditor.OverriddenDisasmPos = default;
                    }
                    else
                    {
                        var caret = textEditor.TextArea.Caret;
                        linePos = caret.Line;
                        columnPos = caret.Column;

                        int lineLen = textEditor.Document.GetLineByNumber(linePos).Length;
                        // If caret is at the end of line
                        if (lineLen == columnPos - 1)
                            columnPos = -1;

                        disasmCodePos = (linePos, columnPos);
                    }
                    #pragma warning restore CA1416

                    LastContentState = new CodeTabState()
                    {
                        MainScrollPosition = mainScrollPos,
                        DecompiledCodePosition = decompCodePos,
                        DisassemblyCodePosition = disasmCodePos,
                        IsDecompiledOpen = isDecompiledOpen
                    };
                    break;

                case UndertaleRoomEditor roomEditor:
                    ScrollViewer roomPreviewViewer = roomEditor.RoomGraphicsScroll;
                    (double Left, double Top) previewScrollPos = (roomPreviewViewer.HorizontalOffset, roomPreviewViewer.VerticalOffset);

                    bool[] objTreeItemsStates = new bool[5]
                    {
                        roomEditor.BGItems.IsExpanded,
                        roomEditor.ViewItems.IsExpanded,
                        roomEditor.GameObjItems.IsExpanded,
                        roomEditor.TileItems.IsExpanded,
                        roomEditor.LayerItems.IsExpanded
                    };
                    ScrollViewer treeObjViewer = MainWindow.FindVisualChild<ScrollViewer>(roomEditor.RoomObjectsTree);
                    (double Left, double Top) treeScrollPos = (treeObjViewer.HorizontalOffset, treeObjViewer.VerticalOffset);
                    object selectedObj = roomEditor.RoomObjectsTree.SelectedItem;
                    if (selectedObj is TreeViewItem item)
                        selectedObj = item.DataContext;

                    LastContentState = new RoomTabState()
                    {
                        RoomPreviewScrollPosition = previewScrollPos,
                        RoomPreviewTransform = roomEditor.RoomGraphics.LayoutTransform,
                        ObjectTreeItemsStates = objTreeItemsStates,
                        ObjectsTreeScrollPosition = treeScrollPos,
                        SelectedObject = selectedObj
                    };
                    break;

                case UndertaleFontEditor fontEditor:
                    UndertaleFont.Glyph glyph = null;
                    if (fontEditor.GlyphsGrid.SelectedItem is not null)
                    {
                        glyph = fontEditor.GlyphsGrid.SelectedItem as UndertaleFont.Glyph;
                        if (glyph is null)
                        {
                            Debug.WriteLine("Can't save the selected glyph of the font editor - \"SelectedItem\" is not a glyph.");
                            return;
                        }
                    }
                    
                    ScrollViewer glyphsViewer = MainWindow.FindVisualChild<ScrollViewer>(fontEditor.GlyphsGrid);
                    if (glyphsViewer is null)
                    {
                        Debug.WriteLine("Can't save the scroll position of the font editor glyphs - \"ScrollViewer\" is not found.");
                        return;
                    }

                    LastContentState = new FontTabState()
                    {
                        MainScrollPosition = mainScrollPos,
                        SelectedGlyph = glyph,
                        GlyphsScrollPosition = glyphsViewer.VerticalOffset
                    };
                    break;

                default:
                    LastContentState = new()
                    {
                        MainScrollPosition = mainScrollPos
                    };
                    break;
            }
        }

        /// <summary>Restores the last tab content state.</summary>
        /// <param name="dataEditor">A reference to the object editor of main window.</param>
        public void RestoreTabContentState(ContentControl dataEditor)
        {
            if (dataEditor is null
                || dataEditor.Content is null
                || dataEditor.Content is DescriptionView
                || LastContentState is null)
                return;

            UserControl editor;
            try
            {
                // Wait until the new editor layout will be loaded
                dataEditor.UpdateLayout();

                var contPres = VisualTreeHelper.GetChild(dataEditor, 0);
                editor = (UserControl)VisualTreeHelper.GetChild(contPres, 0);
            }
            catch
            {
                mainWindow.ShowWarning("The last tab content state can't be restored - \"UserControl\" is not found.");
                return;
            }

            ScrollViewer mainScrollViewer = MainWindow.GetNearestParent<ScrollViewer>(dataEditor);
            if (mainScrollViewer is null)
            {
                mainWindow.ShowWarning("The last tab content state can't be restored - \"ScrollViewer\" is not found.");
                return;
            }
            mainScrollViewer.ScrollToVerticalOffset(LastContentState.MainScrollPosition);

            // if "LastContentState" is an instance of "TabContentState" (e.g. not "CodeTabState")
            if (!LastContentState.GetType().IsSubclassOf(typeof(TabContentState)))
            {
                LastContentState = null;
                return;
            }

            switch (LastContentState)
            {
                case CodeTabState codeTabState:
                    // Is executed only if it's the same code entry (between new and old tabs)
                    #pragma warning disable CA1416
                    if (!codeTabState.IsStateRestored)
                        (editor as UndertaleCodeEditor).RestoreState(codeTabState);
                    #pragma warning restore CA1416
                    break;

                case RoomTabState roomTabState:
                    var roomEditor = editor as UndertaleRoomEditor;

                    roomEditor.RoomGraphics.LayoutTransform = roomTabState.RoomPreviewTransform;
                    roomEditor.RoomGraphics.UpdateLayout();

                    ScrollViewer roomPreviewViewer = roomEditor.RoomGraphicsScroll;
                    roomPreviewViewer.ScrollToHorizontalOffset(roomTabState.RoomPreviewScrollPosition.Left);
                    roomPreviewViewer.ScrollToVerticalOffset(roomTabState.RoomPreviewScrollPosition.Top);

                    // (Sadly, arrays don't support destructuring like tuples)
                    roomEditor.BGItems.IsExpanded = roomTabState.ObjectTreeItemsStates[0];
                    roomEditor.ViewItems.IsExpanded = roomTabState.ObjectTreeItemsStates[1];
                    roomEditor.GameObjItems.IsExpanded = roomTabState.ObjectTreeItemsStates[2];
                    roomEditor.TileItems.IsExpanded = roomTabState.ObjectTreeItemsStates[3];
                    roomEditor.LayerItems.IsExpanded = roomTabState.ObjectTreeItemsStates[4];
                    roomEditor.RoomRootItem.UpdateLayout();

                    // Select the object
                    if (roomTabState.SelectedObject is not UndertaleRoom)
                    {
                        TreeViewItem objList = null;
                        Layer layer = null;
                        switch (roomTabState.SelectedObject)
                        {
                            case Background:
                                objList = roomEditor.BGItems;
                                break;

                            case View:
                                objList = roomEditor.ViewItems;
                                break;

                            case GameObject gameObj:
                                var room = roomEditor.DataContext as UndertaleRoom;
                                if (room.Flags.HasFlag(RoomEntryFlags.IsGMS2))
                                {
                                    layer = room.Layers
                                                .FirstOrDefault(l => l.LayerType is LayerType.Instances
                                                    && (l.InstancesData.Instances?.Any(x => x.InstanceID == gameObj.InstanceID) ?? false));
                                    objList = roomEditor.LayerItems.ItemContainerGenerator.ContainerFromItem(layer) as TreeViewItem;
                                }
                                else
                                    objList = roomEditor.GameObjItems;
                                break;

                            case Tile tile:
                                room = roomEditor.DataContext as UndertaleRoom;
                                if (room.Flags.HasFlag(RoomEntryFlags.IsGMS2))
                                {
                                    layer = room.Layers
                                                .FirstOrDefault(l => l.LayerType is LayerType.Assets
                                                    && (l.AssetsData.LegacyTiles?.Any(x => x.InstanceID == tile.InstanceID) ?? false));
                                    objList = roomEditor.LayerItems.ItemContainerGenerator.ContainerFromItem(layer) as TreeViewItem;
                                }
                                else
                                    objList = roomEditor.TileItems;
                                break;

                            case Layer:
                                objList = roomEditor.LayerItems;
                                break;

                            case SpriteInstance spr:
                                room = roomEditor.DataContext as UndertaleRoom;
                                layer = room.Layers
                                            .FirstOrDefault(l => l.LayerType is LayerType.Assets
                                                && (l.AssetsData.Sprites?.Any(x => x.Name == spr.Name) ?? false));
                                objList = roomEditor.LayerItems.ItemContainerGenerator.ContainerFromItem(layer) as TreeViewItem;
                                break;
                        }
                        if (objList is null)
                            return;

                        objList.IsExpanded = true;
                        objList.BringIntoView();
                        objList.UpdateLayout();

                        TreeViewItem objItem = objList?.ItemContainerGenerator.ContainerFromItem(roomTabState.SelectedObject) as TreeViewItem;
                        if (objItem is null)
                            return;
                        objItem.IsSelected = true;
                        objItem.Focus();

                        roomEditor.RoomRootItem.UpdateLayout();
                    }

                    ScrollViewer treeObjViewer = MainWindow.FindVisualChild<ScrollViewer>(roomEditor.RoomObjectsTree);
                    treeObjViewer.ScrollToHorizontalOffset(roomTabState.ObjectsTreeScrollPosition.Left);
                    treeObjViewer.ScrollToVerticalOffset(roomTabState.ObjectsTreeScrollPosition.Top);
                    treeObjViewer.UpdateLayout();
                    break;

                case FontTabState fontTabState:
                    var fontEditor = editor as UndertaleFontEditor;

                    ScrollViewer glyphsViewer = MainWindow.FindVisualChild<ScrollViewer>(fontEditor.GlyphsGrid);
                    if (glyphsViewer is null)
                    {
                        Debug.WriteLine("Can't restore the scroll position of the font editor glyphs - \"ScrollViewer\" is not found.");
                        return;
                    }
                    glyphsViewer.ScrollToVerticalOffset(fontTabState.GlyphsScrollPosition);

                    fontEditor.GlyphsGrid.SelectedItem = fontTabState.SelectedGlyph;
                    break;

                default:
                    Debug.WriteLine($"The content state of a tab \"{this}\" is unknown?");
                    break;
            }

            LastContentState = null;
        }


        /// <summary>
        /// Prepares the code editor before opening the code entry
        /// by setting a corresponding mode ("Decompiled" or "Disassembly") and restoring the code positions.
        /// </summary>
        /// <remarks>Does nothing if it's not a code tab.</remarks>
        public void PrepareCodeEditor()
        {
            if (LastContentState is CodeTabState codeTabState)
            {
                #pragma warning disable CA1416
                if (codeTabState.IsDecompiledOpen)
                    MainWindow.CodeEditorDecompile = MainWindow.CodeEditorMode.Decompile;
                else
                    MainWindow.CodeEditorDecompile = MainWindow.CodeEditorMode.DontDecompile;

                UndertaleCodeEditor.OverriddenDecompPos = codeTabState.DecompiledCodePosition;
                UndertaleCodeEditor.OverriddenDisasmPos = codeTabState.DisassemblyCodePosition;

                codeTabState.IsStateRestored = true;
                #pragma warning restore CA1416
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            // for ease of debugging
            return GetType().FullName + " - {" + CurrentObject?.ToString() + '}';
        }
    }


    /// <summary>A base class for the information of a tab content state.</summary>
    public class TabContentState
    {
        /// <summary>The scroll position of the object editor.</summary>
        public double MainScrollPosition;
    }

    /// <summary>Stores the information about the tab with a code.</summary>
    public class CodeTabState : TabContentState
    {
        /// <summary>The decompiled code position.</summary>
        public (int Line, int Column) DecompiledCodePosition;

        /// <summary>The disassembly code position.</summary>
        public (int Line, int Column) DisassemblyCodePosition;

        /// <summary>Whether the "Decompiled" tab is open.</summary>
        public bool IsDecompiledOpen;

        /// <summary>Whether this state was already restored (applied to the code editor).</summary>
        public bool IsStateRestored;
    }

    /// <summary>Stores the information about the tab with a room.</summary>
    public class RoomTabState : TabContentState
    {
        /// <summary>The scroll position of the room editor preview.</summary>
        public (double Left, double Top) RoomPreviewScrollPosition;

        /// <summary>The scale of the room editor preview.</summary>
        public Transform RoomPreviewTransform;

        /// <summary>The scroll position of the room objects tree.</summary>
        public (double Left, double Top) ObjectsTreeScrollPosition;

        /// <summary>The states of the room objects tree items.</summary>
        /// <remarks>
        /// An order of the states is following:
        /// Backgrounds, views, game objects, tiles, layers.
        /// </remarks>
        public bool[] ObjectTreeItemsStates;

        /// <summary>The selected room object.</summary>
        public object SelectedObject;
    }

    /// <summary>Stores the information about the tab with a font.</summary>
    public class FontTabState : TabContentState
    {
        /// <summary>The selected font glyph.</summary>
        public UndertaleFont.Glyph SelectedGlyph;

        /// <summary>The scroll position of the glyphs grid.</summary>
        public double GlyphsScrollPosition;
    }


    /// <summary>A converter that generates the tab title from the tab reference.</summary>
    public class TabTitleConverter : IMultiValueConverter
    {
        /// <summary>A static instance of <see cref="TabTitleConverter"/>.</summary>
        public static TabTitleConverter Instance { get; } = new();

        /// <inheritdoc/>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is not Tab tab)
                return null;

            if (!tab.IsCustomTitle)
                tab.TabTitle = Tab.GetTitleForObject(tab.CurrentObject);

            return tab.TabTitle;
        }

        /// <inheritdoc/>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
