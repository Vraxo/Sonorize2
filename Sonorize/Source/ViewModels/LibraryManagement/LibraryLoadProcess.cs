﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using Sonorize.Models;

namespace Sonorize.ViewModels.LibraryManagement;

public class LibraryLoadProcess
{
    private readonly LibraryComponentProvider _components;
    private readonly Action _applyFilterDelegate;
    private readonly Action _updateStatusBarTextDelegate;
    private readonly Action<bool> _setLoadingFlagDelegate;
    private readonly Action<string> _setStatusTextDelegate;
    private readonly Dispatcher _uiDispatcher;

    public LibraryLoadProcess(
        LibraryComponentProvider components,
        Action applyFilterDelegate,
        Action updateStatusBarTextDelegate,
        Action<bool> setLoadingFlagDelegate,
        Action<string> setStatusTextDelegate,
        Dispatcher uiDispatcher)
    {
        _components = components ?? throw new ArgumentNullException(nameof(components));
        _applyFilterDelegate = applyFilterDelegate ?? throw new ArgumentNullException(nameof(applyFilterDelegate));
        _updateStatusBarTextDelegate = updateStatusBarTextDelegate ?? throw new ArgumentNullException(nameof(updateStatusBarTextDelegate));
        _setLoadingFlagDelegate = setLoadingFlagDelegate ?? throw new ArgumentNullException(nameof(setLoadingFlagDelegate));
        _setStatusTextDelegate = setStatusTextDelegate ?? throw new ArgumentNullException(nameof(setStatusTextDelegate));
        _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
    }

    public async Task ExecuteLoadAsync()
    {
        _setLoadingFlagDelegate(true);
        _components.FilterState.ClearSelectionsAndSearch();

        await _uiDispatcher.InvokeAsync(() =>
        {
            _components.SongList.ClearAllSongs();
            _components.Groupings.Artists.Clear();
            _components.Groupings.Albums.Clear();
            _setStatusTextDelegate("Preparing to load music...");
        });

        // This local list is only used to pass to songAddedCallback, which is then passed to DataOrchestrator.
        // DataOrchestrator itself now returns the fully loaded list, so this local list is not strictly necessary for the final result.
        List<Song> loadedRawSongsAccumulator = new List<Song>();
        Action<Song> songAddedCallback = song => loadedRawSongsAccumulator.Add(song);
        Action<string> statusUpdateCallback = status => _setStatusTextDelegate(status);

        var allLoadedSongsFromOrchestrator = await _components.DataOrchestrator.LoadAndProcessLibraryDataAsync(statusUpdateCallback, songAddedCallback);
        _components.SongList.SetAllSongs(allLoadedSongsFromOrchestrator);

        await _uiDispatcher.InvokeAsync(() =>
        {
            _components.Groupings.PopulateCollections(_components.SongList.GetAllSongsReadOnly());
            _applyFilterDelegate();
        });

        _setLoadingFlagDelegate(false);
        _updateStatusBarTextDelegate();
    }
}