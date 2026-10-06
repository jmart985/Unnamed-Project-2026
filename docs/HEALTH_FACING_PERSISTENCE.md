# Player health and facing persistence

Issue #302 extends the existing World snapshot contract to preserve the player’s gameplay-critical injury state and horizontal facing. Each humanoid body part stores its current health together with its validated maximum and consciousness flag. The snapshot also stores the pending head-death countdown and the authoritative dead flag. Presentation-only camera pitch is deliberately excluded.

The player’s facing is a finite, unit, horizontal `Vector3` direction. Loading applies it to the Godot player controller’s yaw after the World has passed full validation. Older snapshots migrate to full health, an alive state with no pending death, and the default forward direction. Invalid body-part names, health ranges, death timers, or facing vectors are rejected before the live World is changed.

Validation is covered by `WorldSnapshotTests`: damaged health, facing, and death metadata round-trip; malformed facing preserves the existing World; and the complete simulation suite remains a required regression check. NPC and creature health persistence, camera pitch persistence, death UI, and new combat rules remain outside this focused correction.
