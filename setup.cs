// Cross-platform template setup script (macOS / Linux / Windows).
//
// Run from the repo root with the .NET 10 SDK (already prerequisite #1):
//
//     dotnet run setup.cs                 # interactive
//     dotnet run setup.cs -- --dry-run    # show planned changes, write nothing
//     dotnet run setup.cs -- --yes        # accept all defaults, non-interactive
//     ./setup.sh        (macOS/Linux)     # thin wrapper around the above
//     ./setup.ps1       (Windows)         # thin wrapper around the above
//
// It does the deterministic, mechanical part of turning this template into your project:
// renaming the namespace/assembly prefix, the analyzer diagnostic prefix, the frontend
// folder, the Aspire resource names, the database name, and a few branding strings.
//
// The *judgemental* part — scaffolding real modules for your kind of app, regenerating EF
// migrations and the OpenAPI/TS client, rewriting the README — is handled by the AI skill.
// See docs/template-setup.md. Run this script first, then invoke the skill.

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

// ---------------------------------------------------------------------------------------
// Argument parsing
// ---------------------------------------------------------------------------------------
var flags = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
foreach (var raw in args)
{
    if (!raw.StartsWith("--"))
        continue;
    var body = raw[2..];
    var eq = body.IndexOf('=');
    if (eq >= 0)
        flags[body[..eq]] = body[(eq + 1)..];
    else
        flags[body] = null;
}

var dryRun = flags.ContainsKey("dry-run");
var assumeYes = flags.ContainsKey("yes") || flags.ContainsKey("y");
var nonInteractive = assumeYes || !Environment.UserInteractive || Console.IsInputRedirected;

if (flags.ContainsKey("help") || flags.ContainsKey("h"))
{
    Console.WriteLine(
        """
        Template setup — rebrand this template into your own project.

        Usage:
          dotnet run setup.cs [-- options]

        Options:
          --target=<dir>             Where to create the project. Empty/"." = rename in place;
                                     any other path copies the template there first, then renames.
          --prefix=<Name>            Namespace/assembly/folder prefix (PascalCase, e.g. Contoso)
          --analyzer-prefix=<NAME>   Analyzer diagnostic-id prefix (e.g. CTSO -> CTSO001)
          --web=<name>               Frontend folder + pnpm + Aspire resource name (kebab)
          --api=<name>               Aspire API resource name (kebab)
          --db=<name>                Database name — also the Aspire resource name (kebab; no underscores)
          --db-password=<value>      Local-dev Postgres password (local only)
          --title=<text>            Project display title (README H1)
          --license-holder=<text>    Copyright holder for LICENSE
          --reinit-git               Wipe template git history and create a fresh initial commit
          --keep-tooling             Keep setup.cs / wrappers / setup skill files afterwards
          --dry-run                  Print planned changes, write nothing
          --yes, -y                  Accept all defaults, no prompts
          --help, -h                 Show this help
        """
    );
    return 0;
}

var sourceRoot = Directory.GetCurrentDirectory();
if (!File.Exists(Path.Combine(sourceRoot, "Acme.slnx")))
{
    Console.Error.WriteLine(
        "error: Acme.slnx not found in the current directory.\n"
            + "       Run this from the template repo root (where Acme.slnx lives).\n"
            + "       If you've already run setup, there's nothing to do."
    );
    return 1;
}

Console.WriteLine("== Awesome Opinionated Template — setup ==");
if (dryRun)
    Console.WriteLine("(dry run — no files will be changed)\n");

// ---------------------------------------------------------------------------------------
// Collect configuration (flags > prompts > defaults)
// ---------------------------------------------------------------------------------------
string Ask(string flag, string label, string @default, Func<string, string?>? validate = null)
{
    while (true)
    {
        string value;
        if (flags.TryGetValue(flag, out var f) && f is not null)
            value = f;
        else if (nonInteractive)
            value = @default;
        else
        {
            Console.Write($"{label} [{@default}]: ");
            var input = Console.ReadLine();
            value = string.IsNullOrWhiteSpace(input) ? @default : input.Trim();
        }

        var error = validate?.Invoke(value);
        if (error is null)
            return value;

        Console.Error.WriteLine($"  ! {error}");
        if (nonInteractive)
            Environment.Exit(1);
    }
}

string? ValidatePrefix(string v) =>
    Regex.IsMatch(v, "^[A-Za-z][A-Za-z0-9]*$")
        ? null
        : "Prefix must start with a letter and contain only letters/digits (PascalCase, e.g. Contoso).";

string? ValidateKebab(string v) =>
    Regex.IsMatch(v, "^[a-z][a-z0-9-]*$")
        ? null
        : "Use lowercase letters, digits and hyphens (e.g. contoso-web).";

var targetInput = Ask(
    "target",
    "Create the project in (absolute or relative path; empty = rename in place)",
    "",
    _ => null
);
var copying =
    !string.IsNullOrWhiteSpace(targetInput) && Path.GetFullPath(targetInput) != sourceRoot;
var targetRoot = copying ? Path.GetFullPath(targetInput) : sourceRoot;

var prefix = Ask(
    "prefix",
    "Namespace / assembly / .slnx prefix (PascalCase)",
    "Acme",
    ValidatePrefix
);
var kebab = Regex.Replace(prefix, "(?<=.)([A-Z])", "-$1").ToLowerInvariant();

var analyzerPrefix = Ask(
    "analyzer-prefix",
    "Analyzer diagnostic-id prefix (e.g. CTSO -> CTSO001)",
    prefix.ToUpperInvariant(),
    v => Regex.IsMatch(v, "^[A-Z][A-Z0-9]*$") ? null : "Use uppercase letters/digits (e.g. CTSO)."
);

var web = Ask("web", "Frontend folder / pnpm / web resource name", $"{kebab}-web", ValidateKebab);
var api = Ask("api", "Aspire API resource name", $"{kebab}-api", ValidateKebab);

// The db name doubles as the Aspire resource name in AddDatabase(...), which only permits
// ASCII letters, digits and hyphens (ASPIRE006) — so it must be kebab, never snake_case.
var db = Ask("db", "Database / Aspire resource name (kebab)", kebab, ValidateKebab);
var dbPassword = Ask(
    "db-password",
    "Local-dev Postgres password (local only)",
    $"{kebab}-dev",
    _ => null
);
var title = Ask("title", "Project display title (README H1)", prefix, _ => null);

var gitUser = Run("git", "config user.name")?.Trim();
var licenseHolder = Ask(
    "license-holder",
    "Copyright holder (LICENSE)",
    string.IsNullOrWhiteSpace(gitUser) ? "Bas Nijhuis" : gitUser,
    _ => null
);

bool AskYesNo(string flag, string label, bool @default)
{
    if (flags.ContainsKey(flag))
        return true;
    if (nonInteractive)
        return @default;
    Console.Write($"{label} [{(@default ? "Y/n" : "y/N")}]: ");
    var input = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (string.IsNullOrEmpty(input))
        return @default;
    return input is "y" or "yes";
}

var reinitGit = AskYesNo(
    "reinit-git",
    copying
        ? "Initialize a fresh git repo in the new project?"
        : "Wipe template git history and start fresh?",
    @default: copying
);
var removeTooling =
    !flags.ContainsKey("keep-tooling")
    && AskYesNo("remove-tooling", "Remove setup tooling (script + setup skill) when done?", false);

// ---------------------------------------------------------------------------------------
// Resolve where the rename runs. In place: the current directory. Otherwise copy the
// template into the target first (skipping VCS/build artifacts) and rename there, leaving
// the original template untouched. Dry run never copies — it previews against the source.
// ---------------------------------------------------------------------------------------
var copyExcludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    ".git",
    "node_modules",
    "bin",
    "obj",
    "dist",
    "build",
    ".react-router",
};

void CopyTree(string from, string to)
{
    Directory.CreateDirectory(to);
    foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
    {
        var rel = Path.GetRelativePath(from, dir);
        if (
            rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(copyExcludes.Contains)
        )
            continue;
        Directory.CreateDirectory(Path.Combine(to, rel));
    }
    foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
    {
        var rel = Path.GetRelativePath(from, file);
        var parts = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Any(copyExcludes.Contains) || rel == "packages.lock.json")
            continue;
        var dest = Path.Combine(to, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.Copy(file, dest, overwrite: true);
    }
}

if (copying)
{
    var exists =
        Directory.Exists(targetRoot) && Directory.EnumerateFileSystemEntries(targetRoot).Any();
    if (exists && !assumeYes)
    {
        Console.Error.WriteLine(
            $"error: target '{targetRoot}' already exists and is not empty.\n"
                + "       Choose an empty/new directory, or pass --yes to copy into it anyway."
        );
        return 1;
    }
    if (dryRun)
        Console.WriteLine(
            $"[dry-run] would copy the template to {targetRoot}, then rename there\n"
        );
    else
    {
        Console.WriteLine($"copying template -> {targetRoot}");
        CopyTree(sourceRoot, targetRoot);
    }
}

// The rename operates on the target when we actually copied; otherwise on the source (also
// the read-only surface for a dry run, so the preview reflects the real files).
var repoRoot = (copying && !dryRun) ? targetRoot : sourceRoot;

// ---------------------------------------------------------------------------------------
// Replacement plan. Order matters: the most specific lowercase compounds first, then the
// bare db name (word-boundary), then the all-caps analyzer prefix, then the PascalCase
// prefix as a plain substring (so AcmeUnitOfWork, Acme_Api, Acme.Kernel all flow through).
// ---------------------------------------------------------------------------------------
var literalReplacements = new List<(string From, string To)>
{
    ("acme-web", web), // also covers "acme-web-prepare" -> "<web>-prepare"
    ("acme-api", api),
    ("acme-dev", dbPassword),
    ("Awesome Opinionated Template", title),
};

string ApplyReplacements(string text)
{
    foreach (var (from, to) in literalReplacements)
        if (from != to)
            text = text.Replace(from, to);

    // Bare PostgreSQL database name (AddDatabase("acme") / GetConnectionString("acme")).
    if (db != "acme")
        text = Regex.Replace(text, @"\bacme\b", db);

    // Analyzer diagnostic-id prefix (ACME001 ...). All-caps, distinct from "Acme".
    if (analyzerPrefix != "ACME")
        text = text.Replace("ACME", analyzerPrefix);

    // Namespace / assembly / folder prefix. Distinctive PascalCase token — substring is safe.
    if (prefix != "Acme")
        text = text.Replace("Acme", prefix);

    return text;
}

string ApplyPathReplacements(string segment)
{
    if (web != "acme-web")
        segment = segment.Replace("acme-web", web);
    if (prefix != "Acme")
        segment = segment.Replace("Acme", prefix);
    return segment;
}

// ---------------------------------------------------------------------------------------
// File set. Prefer git-tracked files (auto-excludes node_modules/bin/obj/build); otherwise
// walk the tree with the same exclusions.
// ---------------------------------------------------------------------------------------
var skipPrefixes = new[]
{
    "setup.cs",
    "setup.test.cs",
    "setup.sh",
    "setup.ps1",
    ".claude/",
    ".cursor/",
    ".github/prompts/",
    ".github/copilot-instructions.md",
    "AGENTS.md",
    "GEMINI.md",
    "docs/template-setup.md",
    ".git/",
    "node_modules/",
}; // setup tooling: never rewrite (keeps its example tokens intact) and never rename.

var binaryExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    ".ico",
    ".png",
    ".jpg",
    ".jpeg",
    ".gif",
    ".webp",
    ".woff",
    ".woff2",
    ".ttf",
    ".eot",
    ".pdf",
    ".zip",
    ".gz",
    ".dll",
    ".exe",
    ".pfx",
    ".snk",
    ".DS_Store",
};

var noContentRewrite = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "packages.lock.json",
    "pnpm-lock.yaml",
}; // regenerated by restore / pnpm install; don't touch contents (but do rename if path matches).

bool IsSkipped(string rel) =>
    skipPrefixes.Any(p =>
        rel.Equals(p, StringComparison.OrdinalIgnoreCase)
        || rel.StartsWith(p, StringComparison.OrdinalIgnoreCase)
    );

List<string> GetFiles()
{
    var tracked = Run("git", $"-C \"{repoRoot}\" ls-files");
    if (tracked is not null)
        return tracked
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    var excludedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        "node_modules",
        "bin",
        "obj",
        "dist",
        "build",
        ".react-router",
    };
    var results = new List<string>();
    void Walk(string dir)
    {
        foreach (var sub in Directory.GetDirectories(dir))
            if (!excludedDirs.Contains(Path.GetFileName(sub)))
                Walk(sub);
        foreach (var file in Directory.GetFiles(dir))
            results.Add(Path.GetRelativePath(repoRoot, file).Replace('\\', '/'));
    }
    Walk(repoRoot);
    return results;
}

var files = GetFiles().Where(f => !IsSkipped(f)).ToList();

// ---------------------------------------------------------------------------------------
// Pass 1 — rewrite file contents.
// ---------------------------------------------------------------------------------------
int contentChanged = 0;
foreach (var rel in files)
{
    var ext = Path.GetExtension(rel);
    var name = Path.GetFileName(rel);
    if (binaryExtensions.Contains(ext) || noContentRewrite.Contains(name))
        continue;

    var full = Path.Combine(repoRoot, rel.Replace('/', Path.DirectorySeparatorChar));
    if (!File.Exists(full))
        continue;

    var bytes = File.ReadAllBytes(full);
    if (Array.IndexOf(bytes, (byte)0) >= 0)
        continue; // looks binary

    var original = Encoding.UTF8.GetString(bytes);
    var updated = ApplyReplacements(original);

    // LICENSE holder is a one-off targeted replacement.
    if (name == "LICENSE" && licenseHolder != "Bas Nijhuis")
        updated = updated.Replace("Bas Nijhuis", licenseHolder);

    if (updated != original)
    {
        contentChanged++;
        if (dryRun)
            Console.WriteLine($"  edit   {rel}");
        else
            File.WriteAllText(full, updated);
    }
}
Console.WriteLine($"{(dryRun ? "[dry-run] " : "")}content updated in {contentChanged} file(s)");

// ---------------------------------------------------------------------------------------
// Pass 2 — rename files/folders whose path contains a token. Deepest paths first so we move
// files before their parent directories disappear.
// ---------------------------------------------------------------------------------------
int renamed = 0;
var isGitRepo = Run("git", $"-C \"{repoRoot}\" rev-parse --is-inside-work-tree")?.Trim() == "true";
foreach (var rel in files.OrderByDescending(f => f.Count(c => c == '/')))
{
    var newRel = string.Join('/', rel.Split('/').Select(ApplyPathReplacements));
    if (newRel == rel)
        continue;

    var from = Path.Combine(repoRoot, rel.Replace('/', Path.DirectorySeparatorChar));
    var to = Path.Combine(repoRoot, newRel.Replace('/', Path.DirectorySeparatorChar));
    renamed++;
    if (dryRun)
    {
        Console.WriteLine($"  rename {rel}  ->  {newRel}");
        continue;
    }

    Directory.CreateDirectory(Path.GetDirectoryName(to)!);
    if (isGitRepo && Run("git", $"-C \"{repoRoot}\" mv -k \"{rel}\" \"{newRel}\"") is not null)
    {
        // git mv handled it
    }
    else if (File.Exists(from))
    {
        File.Move(from, to, overwrite: true);
    }
}
Console.WriteLine($"{(dryRun ? "[dry-run] " : "")}renamed {renamed} path(s)");

// Remove directories left empty by the renames.
if (!dryRun)
{
    foreach (
        var dir in Directory
            .GetDirectories(repoRoot, "*", SearchOption.AllDirectories)
            .Where(d =>
                !d.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}")
                && !d.EndsWith($"{Path.DirectorySeparatorChar}.git")
            )
            .OrderByDescending(d => d.Length)
    )
    {
        try
        {
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
        }
        catch
        { /* best effort */
        }
    }
}

// ---------------------------------------------------------------------------------------
// Post-actions.
// ---------------------------------------------------------------------------------------
if (!dryRun && removeTooling)
{
    foreach (
        var path in new[]
        {
            "setup.cs",
            "setup.test.cs",
            "setup.sh",
            "setup.ps1",
            ".claude/skills/setup-template",
            ".github/prompts/setup-template.prompt.md",
            ".cursor/rules/setup-template.mdc",
            "docs/template-setup.md",
        }
    )
    {
        var full = Path.Combine(repoRoot, path.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            if (Directory.Exists(full))
                Directory.Delete(full, recursive: true);
            else if (File.Exists(full))
                File.Delete(full);
        }
        catch
        { /* best effort */
        }
    }

    // Strip the "Setup script (tests)" job from the CI workflow so it doesn't fail on the now-missing
    // setup.test.cs. Anchored on the job key (no renamed tokens), so it works after the content pass.
    var ciFile = Path.Combine(repoRoot, ".github", "workflows", "ci.yml");
    if (File.Exists(ciFile))
    {
        var lines = File.ReadAllText(ciFile).Replace("\r\n", "\n").Split('\n').ToList();
        var jobIdx = lines.FindIndex(l => l == "  setup-script:");
        if (jobIdx >= 0)
        {
            // Extend up over the job's leading comment lines, and down to the next job key (a line
            // indented exactly two spaces that isn't a comment) — removing the job and its trailing
            // blank line, leaving one blank line between the surrounding jobs.
            var start = jobIdx;
            while (start > 0 && lines[start - 1].StartsWith("  #"))
                start--;
            var end = jobIdx + 1;
            while (
                end < lines.Count
                && !(
                    lines[end].StartsWith("  ")
                    && lines[end].Length > 2
                    && lines[end][2] != ' '
                    && lines[end][2] != '#'
                )
            )
                end++;
            lines.RemoveRange(start, end - start);
            File.WriteAllText(ciFile, string.Join('\n', lines));
        }
    }

    Console.WriteLine("removed setup tooling");
}

if (!dryRun && reinitGit)
{
    var gitDir = Path.Combine(repoRoot, ".git");
    if (Directory.Exists(gitDir))
        Directory.Delete(gitDir, recursive: true);
    Run("git", $"-C \"{repoRoot}\" init");
    Run("git", $"-C \"{repoRoot}\" add -A");
    Run("git", $"-C \"{repoRoot}\" commit -m \"Initial commit from Awesome Opinionated Template\"");
    Console.WriteLine("re-initialized git history");
}

// ---------------------------------------------------------------------------------------
// Summary + handoff to the AI skill.
// ---------------------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine(
    dryRun ? "Dry run complete. Re-run without --dry-run to apply." : "Rename complete."
);
Console.WriteLine();
Console.WriteLine("Applied:");
Console.WriteLine($"  prefix            Acme      -> {prefix}   (solution: {prefix}.slnx)");
Console.WriteLine($"  analyzer prefix   ACME      -> {analyzerPrefix}");
Console.WriteLine($"  web resource      acme-web  -> {web}");
Console.WriteLine($"  api resource      acme-api  -> {api}");
Console.WriteLine($"  database          acme      -> {db}");
Console.WriteLine($"  title             Awesome Opinionated Template -> {title}");
if (copying)
    Console.WriteLine($"  location          {repoRoot}");
Console.WriteLine();
Console.WriteLine("Next steps:");
if (copying && !dryRun)
    Console.WriteLine($"  0. cd \"{repoRoot}\"");
Console.WriteLine("  1. dotnet restore && (cd src/Services/" + web + " && pnpm install)");
Console.WriteLine("  2. Run the AI setup skill to scaffold modules for your kind of app,");
Console.WriteLine("     regenerate EF migrations + the API client, and rewrite the README.");
Console.WriteLine("       Claude:  /setup-template      Codex/Gemini: see AGENTS.md / GEMINI.md");
Console.WriteLine("       (canonical instructions: docs/template-setup.md)");
return 0;

// ---------------------------------------------------------------------------------------
// Helper: run a process, return stdout or null if it failed / isn't available.
// ---------------------------------------------------------------------------------------
static string? Run(string file, string arguments)
{
    try
    {
        var psi = new ProcessStartInfo(file, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var proc = Process.Start(psi);
        if (proc is null)
            return null;
        var stdout = proc.StandardOutput.ReadToEnd();
        proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        return proc.ExitCode == 0 ? stdout : null;
    }
    catch
    {
        return null;
    }
}
