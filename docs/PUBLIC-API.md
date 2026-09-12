# MultiMP3 public API

Base URL: `https://multimp3.vercel.app/api`

The API accepts credential-free requests from browser, command-line, and server-side clients. Browser CORS is enabled for every origin. The service currently limits clients to 40 requests per minute. Use it only for media you own, have permission to download, or are otherwise legally allowed to save.

## Endpoints

| Method | Path | Request | Response |
| --- | --- | --- | --- |
| `GET` | `/health` | None | JSON service and dependency health |
| `POST` | `/info` | `{"urls":["https://…"]}` with 1–50 supported URLs | JSON `{ "items": [...] }` |
| `POST` | `/download` | `url`, optional `title`, and optional `quality` | `audio/mpeg` MP3 attachment |
| `POST` | `/export` | `albums`, optional `quality`, and optional `filename` | ZIP attachment |

Supported quality values are `128`, `192`, `256`, `320`, and `best`. Export requests may contain 1–50 albums and 1–250 total tracks. URL fields must use HTTPS and a supported YouTube host. Errors return JSON with an `error` string. Downloads can take time because extraction and conversion happen on demand.

## Quick examples

Replace `VIDEO_ID` with a supported public media identifier.

### cURL

```bash
curl https://multimp3.vercel.app/api/health
curl -L -o "Example Track.mp3" -H "Content-Type: application/json" -d '{"url":"https://www.youtube.com/watch?v=VIDEO_ID","title":"Example Track","quality":"192"}' https://multimp3.vercel.app/api/download
```

### JavaScript

```js
const response = await fetch("https://multimp3.vercel.app/api/info", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ urls: ["https://www.youtube.com/watch?v=VIDEO_ID"] })
});
console.log(await response.json());
```

### Python

```python
import requests
r = requests.post("https://multimp3.vercel.app/api/download", json={
    "url": "https://www.youtube.com/watch?v=VIDEO_ID", "title": "Example Track", "quality": "192"
})
r.raise_for_status()
open("Example Track.mp3", "wb").write(r.content)
```

### PowerShell

```powershell
$body = @{ urls = @("https://www.youtube.com/watch?v=VIDEO_ID") } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "https://multimp3.vercel.app/api/info" -ContentType "application/json" -Body $body
```

### C#

```csharp
using var http = new HttpClient();
var json = JsonContent.Create(new { urls = new[] { "https://www.youtube.com/watch?v=VIDEO_ID" } });
var response = await http.PostAsync("https://multimp3.vercel.app/api/info", json);
response.EnsureSuccessStatusCode();
Console.WriteLine(await response.Content.ReadAsStringAsync());
```

### Go

```go
body := strings.NewReader(`{"urls":["https://www.youtube.com/watch?v=VIDEO_ID"]}`)
resp, err := http.Post("https://multimp3.vercel.app/api/info", "application/json", body)
if err != nil { log.Fatal(err) }
defer resp.Body.Close()
io.Copy(os.Stdout, resp.Body)
```

### PHP

```php
$ch = curl_init('https://multimp3.vercel.app/api/info');
curl_setopt_array($ch, [CURLOPT_POST => true, CURLOPT_RETURNTRANSFER => true,
  CURLOPT_HTTPHEADER => ['Content-Type: application/json'],
  CURLOPT_POSTFIELDS => json_encode(['urls' => ['https://www.youtube.com/watch?v=VIDEO_ID']])]);
echo curl_exec($ch);
```

### Ruby

```ruby
uri = URI('https://multimp3.vercel.app/api/info')
response = Net::HTTP.post(uri, { urls: ['https://www.youtube.com/watch?v=VIDEO_ID'] }.to_json, 'Content-Type' => 'application/json')
puts response.body
```

### Java

```java
var request = HttpRequest.newBuilder(URI.create("https://multimp3.vercel.app/api/info"))
    .header("Content-Type", "application/json")
    .POST(HttpRequest.BodyPublishers.ofString("{\"urls\":[\"https://www.youtube.com/watch?v=VIDEO_ID\"]}"))
    .build();
var response = HttpClient.newHttpClient().send(request, HttpResponse.BodyHandlers.ofString());
System.out.println(response.body());
```
