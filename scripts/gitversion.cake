#addin "nuget:?package=Cake.Docker"
#load "./utils.cake"
#reference "Newtonsoft.Json"
#reference "System"
#reference "System.IO"

public class MyGitVersion
{
    private const string Prfx = "MyGitVersion";
    private static string _json;    
    private static Newtonsoft.Json.Linq.JObject _jo;
    private static ICakeContext _context;

    private const string Image = "softwarepioniere/gitversion:4.0.0-beta0014";
    
    public static void Init(ICakeContext context) {
           _context = context;    
    }

    public static string GetJsonString() {
          if (_context == null)
            throw new System.InvalidOperationException("Please Init context first");

        if (string.IsNullOrEmpty(_json))
            throw new System.InvalidOperationException("Please Calculate Version first");

       return _json;
    }
  
    public static Newtonsoft.Json.Linq.JObject GetJson() {
     
        if (string.IsNullOrEmpty(_json))
            throw new System.InvalidOperationException("Please Calculate Version first");

        if (_jo == null)
            _jo = Newtonsoft.Json.Linq.JObject.Parse(_json);

        return _jo;
    }

    public static string GetVersion() {          
        var v = GetJson()["NuGetVersionV2"].ToString();
        _context.Verbose($"{Prfx}:: GetVersion - NuGetVersionV2: {v}");
        return v;
    }

    public static string GetAssemblyVersion() {        
        var v = GetJson()["AssemblySemVer"].ToString();
        _context.Verbose($"{Prfx}:: GetAssemblyVersion - AssemblySemVer: {v}");
        return v;
    }

    public static void WriteArtifacts(ConvertableDirectoryPath directory)
    {
        var file = directory + _context.File("GitVersion.json");
        _context.Verbose($"{Prfx}:: WriteArtifacts - OutputFile: {file.Path.FullPath}");
        var path = file.Path;
        if (_context.FileExists(path))
            _context.DeleteFile(path);
        
         System.IO.File.WriteAllText(path.ToString(), GetJsonString() , System.Text.Encoding.UTF8);        
    }

    public static string Calculate()
    {
        _context.Verbose($"{Prfx}:: Calculate");
        var dir = _context.MakeAbsolute(_context.Directory("."));

        var tfs = IsTfs(_context);
        if (tfs)
        {
            _context.Verbose($"{Prfx}:: Calculate - VSTS Docker Run");
    
            var set = new DockerContainerRunSettings {
                Rm = true,
                Volume = new [] {
                    dir + ":/repo"
                },
                Env = new [] {
                    "TF_BUILD=True"
                }};
        
            _context.DockerRun( set, Image, "" ) ;      

            _context.Verbose($"{Prfx}:: Calculate - Running on VSTS with output");
                using(var process = _context.StartAndReturnProcess("docker"
                    , new ProcessSettings {
                        RedirectStandardOutput = true,
                        Arguments = new ProcessArgumentBuilder()
                        .Append("run")
                        .Append("--rm")
                        .Append($"-v " + dir + ":/repo")
                        .Append("-e TF_BUILD=True")
                        .Append(Image)                                           
                })){
                process.WaitForExit();
                _json = string.Join("", process.GetStandardOutput());           
            }

        }
        else
        {
            _context.Verbose($"{Prfx}:: Calculate - Local Docker Run on {dir}");

            var set = new DockerContainerRunSettings {
                Rm = true,
                Volume = new [] {
                    dir + ":/repo"
                }
            };
      
            _context.DockerRun( set, Image , "" ) ;//, cmd );        

            _context.Verbose($"{Prfx}:: Calculate - Running Local with output");
                using(var process = _context.StartAndReturnProcess("docker"
                    , new ProcessSettings {
                        RedirectStandardOutput = true,
                        Arguments = new ProcessArgumentBuilder()
                        .Append("run")
                        .Append("--rm")
                        .Append($"-v " + dir + ":/repo")
                        .Append(Image)
                })){
                process.WaitForExit();
                _json = string.Join("", process.GetStandardOutput());             
            }
        }
  
        return GetVersion();                
    }
}