// Tests for setup.cs — run with:  dotnet run setup.test.cs
//
// `dotnet run <file>.cs` compiles a single file, so these can't import setup.cs's functions.
// Instead they drive the real script (the single source of truth) as a black box: each case
// builds a throwaway fixture that mirrors the template's tokens, runs setup.cs against it
// non-interactively, and asserts on the resulting files. Exits non-zero if any case fails.

using System.Diagnostics;
using System.Text;

var scriptPath = Path.GetFullPath("setup.cs");
if (!File.Exists(scriptPath))
{
    Console.Error.WriteLine("error: run this from the repo root (setup.cs must be alongside).");
    return 1;
}

var tmpRoot = Path.Combine(Path.GetTempPath(), "setup-tests-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(tmpRoot);

int passed = 0,
    failed = 0;

void Check(string name, bool ok)
{
    if (ok)
    {
        passed++;
        Console.WriteLine($"  ok   {name}");
    }
    else
    {
        failed++;
        Console.WriteLine($"  FAIL {name}");
    }
}

// --- fixture helpers ------------------------------------------------------------------
string NewDir(string name)
{
    var dir = Path.Combine(tmpRoot, name);
    if (Directory.Exists(dir))
        Directory.Delete(dir, recursive: true);
    Directory.CreateDirectory(dir);
    return dir;
}

void Write(string root, string rel, string content)
{
    var full = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
    File.WriteAllText(full, content);
}

bool Has(string root, string rel) =>
    File.Exists(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)))
    || Directory.Exists(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)));

string Read(string root, string rel) =>
    File.ReadAllText(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)));

// Lay down a minimal tree that exercises every token and ordering edge case.
void MakeFixture(string root)
{
    Write(
        root,
        "Acme.slnx",
        "<Solution>\n  <Project Path=\"src/Services/Acme.Api/Acme.Api.csproj\" />\n  <!-- web: src/Services/acme-web -->\n</Solution>\n"
    );
    Write(
        root,
        "src/Services/Acme.Api/Acme.Api.csproj",
        "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <ItemGroup>\n    <ProjectReference Include=\"..\\..\\Kernel\\Acme.Kernel.Infrastructure\\Acme.Kernel.Infrastructure.csproj\" />\n  </ItemGroup>\n</Project>\n"
    );
    Write(
        root,
        "src/Services/Acme.Api/Program.cs",
        "using Acme.Api;\n"
            + "var database = builder.AddDatabase(\"acme\");\n"
            + "var cs = config.GetConnectionString(\"acme\");\n"
            + "builder.AddJavaScriptApp(\"acme-web\", webPath);\n"
            + "builder.AddProject<Projects.Acme_Api>(\"acme-api\");\n"
            + "builder.AddParameter(\"postgres-password\", \"acme-dev\");\n"
    );
    Write(
        root,
        "src/Kernel/Acme.Kernel.Infrastructure/AcmeUnitOfWork.cs",
        "namespace Acme.Kernel.Infrastructure;\n\npublic sealed class AcmeUnitOfWork { }\n"
    );
    Write(
        root,
        "src/BuildingBlocks/Acme.DomainAnalyzers/DiagnosticIds.cs",
        "public const string Prefix = \"ACME\"; // e.g. ACME001\n"
    );
    Write(root, "src/Services/acme-web/package.json", "{ \"name\": \"acme-web\" }\n");
    Write(
        root,
        "src/Services/acme-web/app/routes/home.tsx",
        "export default function Home() { return null; } // acme-web route\n"
    );
    // Root pnpm workspace: the web member path must track the frontend-folder rename; the generic
    // `scripts` / `tests/e2e` members must be left alone (no token to rewrite).
    Write(
        root,
        "pnpm-workspace.yaml",
        "packages:\n  - \"src/Services/acme-web\"\n  - \"scripts\"\n  - \"tests/e2e\"\n"
    );
    Write(root, "LICENSE", "Copyright (c) 2026 Bas Nijhuis\n");
    Write(root, "README.md", "# Awesome Opinionated Template\n");
}

// --- run the real script non-interactively --------------------------------------------
(int exit, string output) RunSetup(string workdir, params string[] setupArgs)
{
    var psi = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = workdir,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        RedirectStandardInput = true, // makes the child non-interactive (Console.IsInputRedirected)
        UseShellExecute = false,
    };
    psi.ArgumentList.Add("run");
    psi.ArgumentList.Add(scriptPath);
    psi.ArgumentList.Add("--");
    foreach (var a in setupArgs)
        psi.ArgumentList.Add(a);

    using var proc = Process.Start(psi)!;
    proc.StandardInput.Close();
    var stdout = proc.StandardOutput.ReadToEnd();
    var stderr = proc.StandardError.ReadToEnd();
    proc.WaitForExit();
    return (proc.ExitCode, stdout + stderr);
}

Console.WriteLine($"Testing {scriptPath}\n");

// === A. Full apply in place, with a database name distinct from the prefix ============
Console.WriteLine("A. full apply (in place)");
{
    var f = NewDir("a");
    MakeFixture(f);
    var (exit, _) = RunSetup(
        f,
        "--yes",
        "--prefix=Contoso",
        "--analyzer-prefix=CTSO",
        "--web=contoso-web",
        "--api=contoso-api",
        "--db=shopdb",
        "--title=Contoso Platform"
    );
    Check("exits 0", exit == 0);
    Check("Acme.slnx renamed to Contoso.slnx", !Has(f, "Acme.slnx") && Has(f, "Contoso.slnx"));
    Check("project folder renamed", Has(f, "src/Services/Contoso.Api/Contoso.Api.csproj"));
    Check(
        "csproj ProjectReference rewritten",
        Read(f, "src/Services/Contoso.Api/Contoso.Api.csproj")
            .Contains("Contoso.Kernel.Infrastructure\\Contoso.Kernel.Infrastructure.csproj")
    );

    var unit = "src/Kernel/Contoso.Kernel.Infrastructure/ContosoUnitOfWork.cs";
    Check("AcmeUnitOfWork.cs renamed (substring in identifier)", Has(f, unit));
    Check(
        "class + namespace rewritten",
        Read(f, unit).Contains("class ContosoUnitOfWork")
            && Read(f, unit).Contains("namespace Contoso.Kernel.Infrastructure;")
    );

    var prog = Read(f, "src/Services/Contoso.Api/Program.cs");
    Check("using rewritten", prog.Contains("using Contoso.Api;"));
    Check("database name rewritten (bare word)", prog.Contains("AddDatabase(\"shopdb\")"));
    Check("connection string name rewritten", prog.Contains("GetConnectionString(\"shopdb\")"));
    Check("web resource rewritten", prog.Contains("AddJavaScriptApp(\"contoso-web\""));
    Check("api resource rewritten", prog.Contains("(\"contoso-api\")"));
    Check("aspire Projects type rewritten", prog.Contains("Projects.Contoso_Api"));
    Check("db password rewritten", prog.Contains("\"contoso-dev\""));
    Check(
        "no leftover Acme/acme/ACME tokens in Program.cs",
        !prog.Contains("Acme") && !prog.Contains("acme") && !prog.Contains("ACME")
    );

    var slnx = Read(f, "Contoso.slnx");
    Check(
        "slnx paths rewritten",
        slnx.Contains("src/Services/Contoso.Api/Contoso.Api.csproj")
            && slnx.Contains("src/Services/contoso-web")
    );

    var diag = Read(f, "src/BuildingBlocks/Contoso.DomainAnalyzers/DiagnosticIds.cs");
    Check(
        "analyzer prefix rewritten",
        diag.Contains("\"CTSO\"") && diag.Contains("CTSO001") && !diag.Contains("ACME")
    );

    Check(
        "frontend folder + package name rewritten",
        Read(f, "src/Services/contoso-web/package.json").Contains("\"name\": \"contoso-web\"")
    );
    Check("frontend nested files moved", Has(f, "src/Services/contoso-web/app/routes/home.tsx"));

    var ws = Read(f, "pnpm-workspace.yaml");
    Check(
        "pnpm workspace web member path rewritten to the new web folder",
        ws.Contains("src/Services/contoso-web") && !ws.Contains("acme-web")
    );
    Check(
        "generic workspace members (scripts, tests/e2e) preserved",
        ws.Contains("\"scripts\"") && ws.Contains("\"tests/e2e\"")
    );

    Check("LICENSE removed on success", !Has(f, "LICENSE"));
    Check("README title rewritten", Read(f, "README.md").Trim() == "# Contoso Platform");
}

// === B. Dry run writes nothing ========================================================
Console.WriteLine("B. dry run");
{
    var f = NewDir("b");
    MakeFixture(f);
    var (exit, output) = RunSetup(f, "--dry-run", "--yes", "--prefix=Zzz");
    Check("exits 0", exit == 0);
    Check("Acme.slnx untouched", Has(f, "Acme.slnx") && !Has(f, "Zzz.slnx"));
    Check("file contents untouched", Read(f, "src/Services/Acme.Api/Program.cs").Contains("Acme"));
    Check("reports planned edits", output.Contains("edit") && output.Contains("rename"));
    Check("LICENSE preserved (dry run writes nothing)", Has(f, "LICENSE"));
    Check("announces dry run", output.Contains("Dry run complete"));
}

// === C. Precondition guard: no Acme.slnx ==============================================
Console.WriteLine("C. missing-template guard");
{
    var f = NewDir("c"); // empty, no Acme.slnx
    var (exit, output) = RunSetup(f, "--yes", "--prefix=Zzz");
    Check("exits non-zero", exit != 0);
    Check("explains why", output.Contains("Acme.slnx not found"));
}

// === D. Invalid prefix rejected =======================================================
Console.WriteLine("D. invalid prefix");
{
    var f = NewDir("d");
    MakeFixture(f);
    var (exit, _) = RunSetup(f, "--yes", "--prefix=9nope");
    Check("exits non-zero", exit != 0);
    Check("nothing renamed", Has(f, "Acme.slnx"));
}

// === E. Default derivation from a multi-word prefix ===================================
Console.WriteLine("E. derived defaults (ShopFront)");
{
    var f = NewDir("e");
    MakeFixture(f);
    var (exit, _) = RunSetup(f, "--yes", "--prefix=ShopFront");
    Check("exits 0", exit == 0);
    Check("slnx uses prefix", Has(f, "ShopFront.slnx"));
    Check("web defaults to <kebab>-web", Has(f, "src/Services/shop-front-web/package.json"));
    var prog = Read(f, "src/Services/ShopFront.Api/Program.cs");
    Check(
        "db defaults to kebab (Aspire-valid, no underscore)",
        prog.Contains("AddDatabase(\"shop-front\")")
    );
    Check("api defaults to <kebab>-api", prog.Contains("(\"shop-front-api\")"));
    Check("web resource derived", prog.Contains("AddJavaScriptApp(\"shop-front-web\""));
    Check(
        "analyzer prefix defaults to upper-cased prefix",
        Read(f, "src/BuildingBlocks/ShopFront.DomainAnalyzers/DiagnosticIds.cs")
            .Contains("SHOPFRONT")
    );
}

// === F. Overlapping prefix doesn't cascade (AcmeCorp contains "Acme") =================
Console.WriteLine("F. overlapping prefix (AcmeCorp)");
{
    var f = NewDir("f");
    MakeFixture(f);
    var (exit, _) = RunSetup(
        f,
        "--yes",
        "--prefix=AcmeCorp",
        "--analyzer-prefix=ACMECORP",
        "--web=acmecorp-web",
        "--api=acmecorp-api",
        "--db=acmecorp"
    );
    Check("exits 0", exit == 0);
    Check("slnx renamed once", Has(f, "AcmeCorp.slnx"));
    var unit = "src/Kernel/AcmeCorp.Kernel.Infrastructure/AcmeCorpUnitOfWork.cs";
    Check("identifier renamed once (no AcmeCorpCorp)", Has(f, unit));
    Check("class renamed once", Read(f, unit).Contains("class AcmeCorpUnitOfWork { }"));
    var prog = Read(f, "src/Services/AcmeCorp.Api/Program.cs");
    Check("aspire type renamed once", prog.Contains("Projects.AcmeCorp_Api"));
    Check("db rewritten once", prog.Contains("AddDatabase(\"acmecorp\")"));
    Check("web resource not mangled", prog.Contains("AddJavaScriptApp(\"acmecorp-web\""));
}

// === G. Copy to a target location, leaving the source untouched =======================
Console.WriteLine("G. copy to target");
{
    var src = NewDir("g-src");
    MakeFixture(src);
    var target = Path.Combine(tmpRoot, "g-out"); // does not exist yet
    var (exit, _) = RunSetup(src, "--yes", "--prefix=Contoso", "--target=" + target);
    Check("exits 0", exit == 0);
    Check("source left untouched", Has(src, "Acme.slnx") && !Has(src, "Contoso.slnx"));
    Check(
        "source contents untouched",
        Read(src, "src/Services/Acme.Api/Program.cs").Contains("Acme")
    );
    Check("target created + renamed", Has(target, "Contoso.slnx"));
    Check("target project renamed", Has(target, "src/Services/Contoso.Api/Contoso.Api.csproj"));
    Check("fresh git repo initialized in target (default when copying)", Has(target, ".git"));
}

// === H. Refuse to copy into a non-empty target without --yes ==========================
Console.WriteLine("H. non-empty target guard");
{
    var src = NewDir("h-src");
    MakeFixture(src);
    var target = NewDir("h-out");
    Write(target, "existing.txt", "do not clobber me\n");
    var (exit, output) = RunSetup(src, "--prefix=Contoso", "--target=" + target); // no --yes
    Check("exits non-zero", exit != 0);
    Check("explains the conflict", output.Contains("already exists and is not empty"));
    Check("existing target file preserved", Read(target, "existing.txt").Contains("clobber"));
    Check("source untouched", Has(src, "Acme.slnx"));
}

// === I. --remove-tooling deletes tooling files and strips the CI job ==================
Console.WriteLine("I. remove tooling");
{
    var f = NewDir("i");
    MakeFixture(f);
    Write(f, "setup.test.cs", "// dummy tooling\n");
    Write(f, ".claude/skills/setup-template/SKILL.md", "dummy\n");
    Write(
        f,
        ".github/workflows/ci.yml",
        "name: CI\n\n"
            + "on:\n  pull_request:\n\n"
            + "jobs:\n"
            + "  backend:\n    name: Backend\n    runs-on: ubuntu-latest\n    steps:\n      - run: echo backend\n\n"
            + "  # Tests the template's own setup script (setup.cs) — self-contained track.\n"
            + "  # Remove this job once the template is a real project.\n"
            + "  setup-script:\n    name: Setup script (tests)\n    runs-on: ubuntu-latest\n    steps:\n      - run: dotnet run setup.test.cs\n\n"
            + "  frontend:\n    name: Frontend\n    runs-on: ubuntu-latest\n    steps:\n      - run: echo frontend\n"
    );

    var (exit, _) = RunSetup(f, "--yes", "--prefix=Contoso", "--remove-tooling");
    Check("exits 0", exit == 0);
    Check("tooling file removed", !Has(f, "setup.test.cs"));
    Check("skill dir removed", !Has(f, ".claude/skills/setup-template"));

    var ci = Read(f, ".github/workflows/ci.yml");
    Check("CI job key removed", !ci.Contains("setup-script:"));
    Check("CI job name removed", !ci.Contains("Setup script (tests)"));
    Check("CI job leading comment removed", !ci.Contains("Tests the template's own setup script"));
    Check("surrounding jobs preserved", ci.Contains("  backend:") && ci.Contains("  frontend:"));
    Check(
        "one blank line left between the surrounding jobs",
        ci.Contains("      - run: echo backend\n\n  frontend:")
    );
}

// --- cleanup --------------------------------------------------------------------------
try
{
    Directory.Delete(tmpRoot, recursive: true);
}
catch
{ /* best effort */
}
File.Delete(Path.Combine(Path.GetDirectoryName(scriptPath)!, "packages.lock.json"));

Console.WriteLine();
Console.WriteLine($"== {passed} passed, {failed} failed ==");
return failed == 0 ? 0 : 1;
