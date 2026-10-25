// SPDX-License-Identifier: GPL-3.0-or-later
//
// Copyright (C) 2026 Tudor Carean
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

// AssemblyInfo.cs - the tests in this assembly do not run in parallel.
//
// WHY. Almost everything tested here touches GLOBAL WPF state, not only objects of its
// own: Application.Current, the threads' culture, and a dispatcher that belongs to a single
// STA thread. Collections run in parallel trample on exactly these things.
//
// It did happen, and it was hard to see because it failed only sometimes: the STA fixture
// closed in the middle of a series run by another collection, Application.Current went from
// "exists" to "has its dispatcher shut down", and the table's rows landed partly on the UI
// thread, partly on the computing thread. ObservableCollection silently loses rows when
// that happens - six frames analysed, five rows in the table, once every few runs.
//
// The code was hardened separately (LightTabViewModel.OnUi keeps a single thread for the
// whole of a run), but that does not make the tests deterministic: they would still find six
// versus five, only for a cleaner reason. The determinism comes from here.
//
// The cost is nil in practice: the whole suite runs in about two seconds.

[assembly: CollectionBehavior(DisableTestParallelization = true)]
