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

// The tests of this assembly do not run in parallel: nearly all of them touch WPF's global
// state (Application.Current, the threads' culture, one STA dispatcher). Collections run in
// parallel trample exactly that, and the failure shows only now and then (FlatCheck learned
// it the hard way: the STA fixture shut down in the middle of another collection's run).
// The whole suite takes seconds, so the cost is nothing.

[assembly: CollectionBehavior(DisableTestParallelization = true)]
