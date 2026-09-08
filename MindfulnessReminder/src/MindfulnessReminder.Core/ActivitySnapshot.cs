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

namespace MindfulnessReminder.Core;

/// <summary>
/// Everything the app knows about what the user is in the middle of, read in one go.
///
/// It is a plain value with no behaviour on purpose: gathering it is a Windows question and
/// belongs in an adapter, while deciding what it means is a rule and belongs in
/// <see cref="SuppressionPolicy"/>. This type is the line between the two.
/// </summary>
public readonly record struct ActivitySnapshot(
    bool MicrophoneInUse,
    bool CameraInUse,
    ForegroundState Foreground)
{
    /// <summary>Nothing going on -- what a machine with the checks switched off looks like.</summary>
    public static ActivitySnapshot Idle => new(false, false, ForegroundState.Normal);

    /// <summary>
    /// Either capture device being live counts the same. A camera on with the microphone
    /// muted in hardware is still a call, and the two are one checkbox to the user.
    /// </summary>
    public bool CaptureInUse => MicrophoneInUse || CameraInUse;
}
