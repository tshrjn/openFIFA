#!/bin/bash
# ONE-TIME (owner's go-ahead needed): copy ios-factory's signing secrets to the openFIFA repo, the same
# set ios-factory's sync_github pushes to tshrjn/ios-factory. Prints names only, never values.
# Needs ~/ios-factory/.secrets/{asc.env,AuthKey_*.p8,dist.p12,dist-p12.pass} (created by ios-setup).
# Unity secrets (UNITY_LICENSE/UNITY_EMAIL/UNITY_PASSWORD) are not handled here; see docs/IPAD_PIPELINE.md.
set -euo pipefail
REPO=${OPENFIFA_REPO:-tshrjn/openFIFA}
FACTORY=${IOS_FACTORY:-$HOME/ios-factory}
S="$FACTORY/.secrets"
for f in asc.env dist.p12 dist-p12.pass; do [ -f "$S/$f" ] || { echo "missing $S/$f: run ios-factory's ios-setup first" >&2; exit 3; }; done
# shellcheck disable=SC1091
set -a; source "$S/asc.env"; [ -f "$FACTORY/factory.env" ] && source "$FACTORY/factory.env"; set +a
: "${ASC_KEY_ID:?}" "${ASC_ISSUER_ID:?}" "${APPLE_TEAM_ID:?APPLE_TEAM_ID missing in factory.env}"
keypath=${ASC_KEY_PATH:-$S/AuthKey_$ASC_KEY_ID.p8}
base64 -w0 "$S/dist.p12" | gh secret set DIST_P12_B64 -R "$REPO"
gh secret set DIST_P12_PASSWORD -R "$REPO" < "$S/dist-p12.pass"
printf %s "$ASC_KEY_ID" | gh secret set ASC_KEY_ID -R "$REPO"
printf %s "$ASC_ISSUER_ID" | gh secret set ASC_ISSUER_ID -R "$REPO"
base64 -w0 "$keypath" | gh secret set ASC_KEY_P8_B64 -R "$REPO"
gh variable set APPLE_TEAM_ID -R "$REPO" --body "$APPLE_TEAM_ID"
echo "set on $REPO: DIST_P12_B64 DIST_P12_PASSWORD ASC_KEY_ID ASC_ISSUER_ID ASC_KEY_P8_B64, variable APPLE_TEAM_ID"
