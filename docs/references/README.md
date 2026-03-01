# Visual Reference Frames

Used for visual reference only — all OpenFifa assets are original.

---

## EA FC 26 Gameplay Deep Dive (11v11)

Source: **EA SPORTS FC 26 Official Gameplay Deep Dive**
([https://www.youtube.com/watch?v=0GE8YCIQF2M](https://www.youtube.com/watch?v=0GE8YCIQF2M))

### Frames (`frames/`)

| File | Description |
|------|-------------|
| `01_broadcast_camera_hud_scoreboard.png` | Broadcast TV angle with scoreboard, minimap, and player indicators |
| `02_goalkeeper_diving_save.png` | Goalkeeper diving save — player model detail, goal net, crowd |
| `03_broadcast_full_pitch_grass_bands.png` | Wide broadcast view showing mowed grass bands and pitch markings |
| `04_ball_closeup_boots_detail.png` | Close-up on ball and player boots — material and texture detail |
| `05_player_jersey_closeup_stadium.png` | Player jersey close-up with stadium and crowd in background |
| `06_midfield_center_circle_advertising.png` | Midfield view with center circle, advertising boards, crowd |
| `07_broadcast_camera_different_stadium.png` | Broadcast camera at a different stadium — varied lighting and crowd |
| `08_free_kick_set_piece.png` | Free kick setup — player wall, shot indicator, set piece camera angle |
| `09_center_circle_wide_view.png` | Wide center circle view — pitch markings, grass texture, player spacing |
| `10_goal_area_attack_corner.png` | Goal area attack from corner — net detail, goalkeeper, defenders |
| `11_attacking_play_near_goal.png` | Attacking play near goal — goal frame, penalty box markings |
| `12_goalkeeper_distribution_pitch.png` | Goalkeeper distribution — pitch detail, grass close-up, crowd stands |
| `13_goal_area_minimap_hud.png` | Goal area action with minimap and full HUD overlay visible |
| `14_ai_vision_comparison_fc25_fc26.png` | AI vision comparison between FC 25 and FC 26 (debug visualization) |

---

## EA FC 26 Rush Mode (5v5 — Primary Reference)

Source: **EA FC 26 Rush Mode PS5 4K Gameplay**
([https://www.youtube.com/watch?v=GZbAaLSjkc8](https://www.youtube.com/watch?v=GZbAaLSjkc8))

Rush mode is the **primary gameplay reference** for OpenFifa — 5v5 (4 outfield + GK), small enclosed pitch, fast-paced arcade action, 5-minute matches.

### Frames (`rush_frames/`)

| File | Description |
|------|-------------|
| `01_rush_mode_menu_lobby.png` | Rush mode selection UI — lobby, mode cards, player dribbling hero art |
| `02_rush_title_splash.png` | "RUSH" branding splash — green neon logo, stadium visible behind |
| `03_kickoff_full_pitch_hud.png` | Kickoff wide view — full small pitch, both teams, center circle, HUD scoreboard (HOM 0-0 AWY), 5-min timer, player name tags |
| `04_midfield_open_play_spacing.png` | Midfield open play — player spacing across pitch, grass bands, broadcast camera ~40° angle, dashed halfway line |
| `05_center_circle_possession.png` | Center circle possession — ball carrier with yellow indicator, "Pass" label, player name tags, 0-0 at 4:38 |
| `06_goal_area_attack_shot.png` | Shot on goal — goalkeeper (yellow kit), goal frame + net, penalty arc, "GOALKEEPEE" label, white team attacking |
| `07_penalty_box_goal_net.png` | Penalty area action — goal net mesh detail, advertising boards (Gillette), GK positioning, tight camera |
| `08_goal_celebration_emotes.png` | Goal celebration — "GOAT"/"GG"/"Cold" emotes, crowd cheering, 1-0 scoreboard, "Joga Bonito" label |
| `09_goalkeeper_save_net_detail.png` | GK save close-up — goal net mesh, "Firefox" emoji rain, controls overlay (Move GK / Switch / Toggle), 1-0 at 3:57 |
| `10_wide_view_both_goals.png` | Wide view post-goal — both goals visible, full pitch layout, goal frame structures, advertising boards, crowd stands |
| `11_halftime_stats_camera.png` | Half-time stats — behind-goal camera, player card (Marmoush 84), "3 Shots / 1 Goals" overlay, net visible |
| `12_goal_splash_branding.png` | "GOAL" celebration splash — "ULTIMATE TEAM RUSH HOM" text, green chevron design, neon branding |
| `13_attacking_pass_indicators.png` | Attacking play with AI cues — "Through"/"Pass" labels on teammates, 4-4 score, penalty area action |
| `14_post_match_awards.png` | Post-match awards — 3 player models in Rush jerseys, ratings (8.7/9.3/8.6), awards (Aggressive Dribbler / Immune to Pressure / Marathon Runner) |

### Key Visual Observations from Rush Mode

- **Pitch**: Small enclosed arena (~half regulation size), mowed grass bands, white dashed halfway line, full penalty areas both ends
- **Goals**: Full-size goal frames with realistic net mesh, white posts
- **Camera**: Elevated broadcast angle ~35-45°, follows ball, smooth panning, shows full pitch width
- **HUD**: Top-center scoreboard (HOM/AWY + score + timer), player name tags above each player, team crests flanking scoreboard, player cards on edges
- **Players**: 5v5 (4 outfield + 1 GK per team), colored kits (dark blue/teal vs white), yellow GK kit, yellow ball indicator on active player
- **AI Indicators**: "Pass"/"Through" labels on teammates showing available options
- **Stadium**: Indoor/enclosed arena feel, neon green "RUSH" branding, advertising boards (Gillette, Intersport, Crunchyroll, Anime1), crowd close to pitch, photographers on sidelines
- **Match**: 5-minute single-half, high scoring (5-4 final), instant restarts after goals

## Usage

These frames serve as visual targets for OpenFifa's art direction.

### Rush Mode (primary — our game mode)
- `rush_frames/03` — our kickoff view: full pitch, both teams, HUD
- `rush_frames/04, 05` — our midfield play: player spacing, camera angle
- `rush_frames/06, 07` — our goal area: net, goalkeeper, penalty markings
- `rush_frames/08, 12` — our celebrations: emotes, splash screen
- `rush_frames/10` — our stadium: both goals, crowd, advertising, enclosed feel
- `rush_frames/13` — our AI indicators: pass/through labels on teammates

### Gameplay Deep Dive (supplementary — higher fidelity details)
- `frames/01, 03, 06, 07, 09` — broadcast camera angles at various stadiums
- `frames/04, 05, 12` — close-up material/texture details (ball, jersey, boots)
- `frames/02, 08, 10, 11, 13` — goal area action and set pieces
