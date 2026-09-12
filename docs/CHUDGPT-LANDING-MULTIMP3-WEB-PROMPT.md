# Prompt for the Codex task building the ChudGPT landing page

You are working in the existing ChudGPT landing-page repository. Update that site to promote the new MultiMP3 Web experience. Inspect the existing landing-page code before editing and preserve its framework, tokens, routes, navigation, deployment configuration, and current app listings. Complete the implementation, test it, commit it, push it, and deploy it through the landing page's existing Vercel project.

Use these verified ChudGPT routes:

- Main site: `https://chudgpt-landing.vercel.app/`
- Apps directory: `https://chudgpt-landing.vercel.app/apps`
- Existing MultiMP3 page: `https://chudgpt-landing.vercel.app/apps/multimp3`
- MultiMP3 Web: `https://multimp3.vercel.app`
- Public source: `https://github.com/ASTRA228b/MultiMP3`

Use `https://multimp3.vercel.app` for every “Open MultiMP3 Web” action. Store it as a configurable constant named `MULTIMP3_WEB_URL` if the landing project centralizes external application URLs.

Present the product identity as “MultiMP3” with “A ChudGPT App” as the secondary label. Explain that MultiMP3 now has two editions:

- MultiMP3 for Windows: the full desktop experience with persistent folders, download history, queue recovery, local purchased-MP3 organization, and deeper settings.
- MultiMP3 Web: the quick browser experience for pasting supported YouTube links, creating albums, choosing MP3 quality, downloading tracks, downloading an album ZIP, and exporting all albums as `MultiMP3_Site_Albums.zip`.

Add a strong “Open MultiMP3 Web” action beside the existing Windows download action. Update the `/apps` MultiMP3 card and `/apps/multimp3` detail page, and add a small homepage promotion if the existing layout has a natural featured-app area. Do not turn the landing page into the MultiMP3 app itself.

Use the existing ChudGPT visual system and the real MultiMP3 waveform/logo asset from the public repository. Keep MultiMP3’s purple audio identity while using ChudGPT’s ecosystem treatment sparingly. Do not invent screenshots, usage numbers, testimonials, release status, or functionality.

Accurately describe the deployment boundary: Vercel hosts the static web interface. Audio metadata, yt-dlp extraction, FFmpeg conversion, and ZIP generation require the separately operated MultiMP3 processing backend. The browser stores only lightweight queue, album, and quality metadata. It does not store downloaded media in localStorage.

The processing backend route map is:

- `GET /api/health` — dependency and service health
- `POST /api/info` — read public YouTube metadata for 1–50 validated URLs
- `POST /api/download` — convert and return one permitted track
- `POST /api/export` — create one album ZIP or `MultiMP3_Site_Albums.zip`

Present `https://multimp3.vercel.app/api` as a public, credential-free developer API that works from browser applications, command-line tools, and server-side programs. It supports cross-origin requests and applies a 40-request-per-minute rate limit. Add a dedicated ChudGPT landing-page route at `/apps/multimp3/api-guide` and link to it from the existing MultiMP3 product page and relevant MultiMP3 app card or action area. Do not redesign or edit unrelated landing-page areas.

The new API guide should contain an “API quick start” with copyable, syntax-highlighted examples for cURL, JavaScript, Python, PowerShell, C#, Go, PHP, Ruby, and Java. Include tabs or another compact language selector, request payloads, response types, validation limits, error handling, and a link to `https://github.com/ASTRA228b/MultiMP3/blob/main/docs/PUBLIC-API.md`. Use `https://multimp3.vercel.app/api/...` in every public example. Add an appropriate canonical URL and page metadata for the new route, then include it in the landing site's sitemap if it is publicly indexable.

Do not publish, display, or hardcode the temporary TryCloudflare backend address in the ChudGPT landing site. MultiMP3 Web already manages its processing-service connection and the temporary tunnel address can change after a backend restart.

Include the responsible-use note: users should download only media they own, have permission to download, or are otherwise legally allowed to save. Do not claim DRM, private-video, authentication, regional-restriction, or paywall bypass.

Verify all links, test normal and narrow layouts, run the landing repository’s existing checks, and publish the updated landing site through its existing Vercel project. Return the changed files, checks run, final deployed URLs, and commit hash.
