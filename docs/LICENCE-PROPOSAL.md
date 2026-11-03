# Proposed licensing for CLEAN - Mod Remover and Holster v3.2

Approved by 2Shay on 28 October 2026 for the private final candidate. The adopted
text is `LICENSE.txt` at the project root. The original draft is retained as history.

Adopted text: `LICENSE.txt`. The earlier
`CLEAN-SOURCE-AVAILABLE-LICENCE-DRAFT.txt` is retained as history. These permit inspection, private builds,
private modifications and free noncommercial redistribution, including labelled forks.
They reserve commercial licensing to the rights holder. This is not an OSI open-source
licence and must not be advertised as open source.

The author may sell separately licensed future versions and their own commercially
licensed code. The free rights granted for an existing released version remain available
to compliant recipients; a future paid version cannot retroactively remove them.
The proposal does not require future source publication or promise updates.

## Scope and old rights

- User confirms Alpha 1 was never released or distributed. Its bundled MIT text alone
  does not establish that the public received the new v3.2 implementation under MIT.
- User confirms public v2.3 was MIT. Recipients retain permission to use, modify,
  redistribute and sell that MIT material with its notice. A v3.2 licence cannot revoke it.
- Reused third-party MIT material remains under MIT. Preserve its complete notice,
  identify its files/sections and owner, and restrict only rights 2Shay controls.
- MIT permits incorporation in differently licensed works, but the new restrictions
  cannot remove the underlying MIT grant. Someone can extract or independently build
  on the MIT portions under MIT. The commercial restriction therefore does not give
  exclusive commercial control over old publicly licensed code.
- Copyright covers protectable expression, not a monopoly on the idea of moving mods.
- The public v3.0 Nexus permissions also allow credited mirrors and modifications.
  Do not assert that the proposed v3.2 terms retroactively prohibit those acts.
- The new whole-package notice must distinguish v3.2 material, reused MIT portions,
  framework components, and branding. Keep the supplied MIT text as provenance and
  retain any applicable third-party notices in the published package.

Primary licence reference: [MIT text](https://opensource.org/license/mit).
Existing permissions: [CLEAN Nexus listing](https://www.nexusmods.com/reddeadredemption2/mods/8084).

## Ownership audit before adoption

The source archive supplies no Git history, contribution agreements, original v2.3
sources, or decompilation input. BUILD.md says v2.3 C#/BAT informed behaviour, v3.0
was decompiled for the UI, and the engine was introduced in v3.1 and extended in v3.2.
This is a provenance statement, not independent proof of ownership.

The public listing credits Tasklet and community contributors. On 27 October 2026,
2Shay confirmed all included code is his, with no code supplied by those contributors.
Preserve their feedback/testing credits. This author attestation resolves the known
contribution question, while the archive supplies no independent development history.
Retain any third-party licence if contrary evidence is later identified. Official RDR2
promotional art was initially excluded, then restored in the private application at
the author's request. The later supplied C/star icon is also included. Both are
separately noticed; the code licence does not establish third-party art/branding rights.

No third-party runtime NuGet libraries appear in the supplied project. Production uses
the Windows .NET Framework BCL. The build-only Microsoft.NETFramework.ReferenceAssemblies.net48
1.0.3 package is private to the build and is not distributed with the executable.
Its upstream package licence must remain with that dependency if it is ever redistributed.
The .NET SDK itself is a build tool, excluded from packages and source snapshots.

## Decisions in the draft

1. Inspection and personal noncommercial builds/modifications: permitted.
2. Free noncommercial source and binary redistribution: permitted with notices,
   corresponding modified source, version/change identification and no false endorsement.
3. Charging, paid access, commercial bundling/services and third-party monetisation of
   CLEAN-derived distributions: require separate written permission from 2Shay.
4. Ordinary hosting advertisements do not disqualify a free mirror. No royalties or
   Donation Points permission for derivative authors is implied.
5. The author's own voluntary PayPal support and author-controlled platform rewards
   are permitted. Donations never purchase access, updates, assistance or features.
6. No automatic conversion to MIT/open source after a date. Future licensing is separate.

## Review state

Approved by the author on 28 October. On 27 October the author confirmed all included code is his; feedback/testing
credits remain. Existing MIT permissions are preserved. Public repository,
releases, tags and site publication remain gated. The licence has not
received professional legal review; it describes the proposed rights, not a legal finding.
