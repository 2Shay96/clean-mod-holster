# Attribution and artwork audit

Updated 29 October 2026. Scope: supplied Alpha 1 archives, author attestations,
public CLEAN material and current release assets. Earlier versions are preserved.

| Component | Evidence | Treatment |
|---|---|---|
| SafetyEngine.cs | BUILD.md claims new v3.1 engine, v3.2 extensions; no history; author confirms all code his on 27 October | Preserve 2Shay attribution; author attestation recorded, not an independent history audit |
| UI/*.cs | BUILD.md says recovered by decompiling v3.0; author confirms code his | Keep provenance and historical public permissions; no outside code contributor identified |
| Public v2.3 | Nexus Optional Files explicitly marks MIT; user confirms | MIT rights survive; obtain historical source to map any reused expressions |
| Tasklet | Public Nexus file credit; author says no contributed code | Preserve feedback/testing credit |
| Community names | Public Nexus description; author says all code his | Preserve feedback/testing credits; no third-party code grant claimed |
| .NET Framework 4.8 | Project BCL references | Required Windows runtime; not bundled |
| ReferenceAssemblies.net48 1.0.3 | Build-only private PackageReference | Build dependency, excluded from packages |
| Header lettering | Chinese Rocks Rg3.104 from official Typodermic download; Desktop EULA v260817 | Two complete fixed SVG compositions; no working font/glyph library. Scope/provenance: HEADER-LETTERING.md |
| Supplied sunrise.png | ARTWORK-NOTICE identifies RDR2 promo image; author requests app-only inclusion 28 Oct | Reintroduced byte-identically into private application header; separately noticed; redistribution/DP permission not established |
| Alpha 1 app.ico | No independent provenance in archive | Not used in current app; preserved upstream |
| Current CLEAN C/star icon | Author-supplied ICO/PNG and generation notes | Imported byte-identically; reported image_gen/Pillow provenance, third-party branding clearance not established |

On 28 October the author supplied a new
red/black C-and-star ICO/PNG and requested its implementation. The files are imported
byte-identically; seven ICO frames verified. Reported image_gen/Pillow provenance and
the third-party branding question are recorded in `docs/ICON-PROVENANCE.md`. The old
procedural icon remains in earlier packages and `assets/history/`.

The supplied icon has 16, 24, 32, 48, 64, 128 and 256 pixel PNG frames.
The previous procedural icon generator remains historical, not the current icon's
source. Earlier geometric header/icon assets remain preserved. The supplied notes
report image_gen creation and Pillow conversion; those steps were not repeated here.
No claim of independent branding clearance or absence of AI assistance is made.

Action icons supplied 29 October: scan/reload declare public domain; trash source
confirmed as FreeSVG Public Domain/CC0. Holster is joede's Modern Store Symbol,
CC BY-SA 3.0, adapted by flattening/scaling/recolouring. Its adaptation remains under
that licence. Originals, hashes, attribution and source-check limits: ACTION-ICONS.md.

Author chooses current artwork for release on 29 October. Take-Two permission enquiry
was sent; only an automated response is recorded, not explicit authorization.
ARTWORK-NOTICE.txt and docs/evidence/take2-auto-reply-review-20261029.md record the
remaining rights/site eligibility question. This does not change the adopted code grant.

Author confirmation on 27 October 2026, answering whether Tasklet/community wrote any
included code: “no...all mine”. This resolves the contribution question by author
attestation. The supplied archive still has no development history; older MIT grants
and any independently identified third-party rights remain unaffected. The author
adopted the proposed code licence on 28 Oct; it does not license the official artwork.

Community credits retained for review: ITsEMEt, CinematicTactics, Yakuzer24,
PrinceArthur2708, Godfrey152, angel_reign, 1RuggedGamer, R3ddslayer, Ronin823,
Chen111111333, MurlkeTink, IkariBrendo. Public page's older display name DaemonShay
must not replace the confirmed author name 2Shay in the new application.

Source origin documentation is preserved in `upstream/alpha1-source/BUILD.md` and
`ARTWORK-NOTICE.txt`; exact imported file hashes are in `docs/evidence/archive-provenance.json`.
Nothing from `_to_delete/`, `upstream/`, `.dotnet/` or game installation files belongs
in the proposed public source snapshot. Applicable MIT notices remain in release notices.

Existing handmade art on Nexus is not evidence that it may be embedded in the executable;
the author's family-member credit should be retained if that asset is later supplied/used.
