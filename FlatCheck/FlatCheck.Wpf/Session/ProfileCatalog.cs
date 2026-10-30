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

// The catalog of lens profiles, loaded a single time for the whole window.
//
// It was the Sintetic tab's; since the Structure tab compares the master's vignetting with the
// lens's profiles, both want it, and two loads would have meant listing the ~3500 Adobe files
// twice (a second or two) and two lists that can differ. MainViewModel builds it and gives it
// to both tabs; the tests give a Func that returns sources made by hand, so the window does not
// depend on what is installed on the machine.

using System;
using System.Threading.Tasks;

namespace FlatCheck.Wpf
{
    /// <summary>The window's one lens-profile catalog, located in the background on first
    /// demand and shared by the tabs.</summary>
    internal sealed class ProfileCatalog
    {
        readonly Func<ProfileSources> _locate;
        readonly object _gate = new();
        Task<ProfileSources> _loading;

        /// <summary>Remembers how to locate the sources; nothing is loaded yet.</summary>
        /// <param name="locate">The locator; null = <see cref="ProfileLocator.Locate()"/>,
        /// the machine's own.</param>
        internal ProfileCatalog(Func<ProfileSources> locate = null)
        {
            _locate = locate ?? ProfileLocator.Locate;
        }

        /// <summary>
        /// The sources, loaded in the background on the first request and remembered. A load
        /// that threw is retried on the next request, it does not stay failed for the whole
        /// life of the window; a folder that could not be listed is not such a failure since
        /// R-115: the locator leaves it out and the load succeeds, and what it found is kept
        /// until the window restarts, as the Structure card says of Adobe's profiles.
        /// </summary>
        /// <returns>The loading task, shared by every caller until it fails or is
        /// cancelled.</returns>
        internal Task<ProfileSources> EnsureAsync()
        {
            lock (_gate)
            {
                if (_loading == null || _loading.IsFaulted || _loading.IsCanceled)
                    _loading = Task.Run(_locate);
                return _loading;
            }
        }

        /// <summary>The sources already loaded, or null if they are not yet.</summary>
        internal ProfileSources Loaded
        {
            get
            {
                lock (_gate) return _loading != null && _loading.IsCompletedSuccessfully ? _loading.Result : null;
            }
        }
    }
}
