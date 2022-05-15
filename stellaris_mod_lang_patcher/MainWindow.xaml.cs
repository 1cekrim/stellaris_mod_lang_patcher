using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;

namespace stellaris_mod_lang_patcher
{
    public partial class MainWindow : Window
    {
        string mod_folder_path = @"C:\Program Files (x86)\Steam\steamapps\workshop\content\281990";
        List<Mod> mods = null;
        private enum ProgramState { INVALID, IDLE, RUNNING, ERROR }
        ProgramState current_state = ProgramState.IDLE;
        object state_lock = new object();

        Dictionary<string, List<string>> force_patch_dict = new Dictionary<string, List<string>> {
            { "korean", new List<string> {
                "2703186360", // MKC Addon: AlphaMod
                "2524944243", // MKC Addon: Gigastructural Engineering & More
                "2420144001", // MKC Addon : Planetary Diversity Korean Translation
                "2524947989", // MKC Addon: NSC2 Season 6
                "2533584571", // MKC Addon: Ethics and Civics Alternative - Redux
                "2789326040", // MKC Addon: Real Space - New Frontiers
                "2547749868", // MKC Addon: Ethics and Civics Classic 3.0
                "2703189043", // MKC Addon: EUTAB - Ethos Unique Techs and Buildings
                "2506141839", // Mod Korean Collection: 모드 한국어 모음
                "2747894657", // Korean Language(Korean name)
            } }
        };

        Dictionary<string, string> file_name_typo_dict = new Dictionary<string, string>
        {
            { "l_koean", "l_korean" }
        };

        private bool ChangeState(ProgramState next_state, ProgramState condition = ProgramState.INVALID)
        {
            lock (state_lock)
            {
                if (condition != ProgramState.INVALID && condition != current_state)
                {
                    return false;
                }

                current_state = next_state;


                Dispatcher.Invoke(() =>
                {
                    switch (current_state)
                    {
                        case ProgramState.IDLE:
                            Title = "Stellaris Mod Language Patcher - Idle";
                            Button_LoadModList.IsEnabled = true;
                            Button_Patch.IsEnabled = mods != null;
                            TextBox_From.IsEnabled = true;
                            TextBox_To.IsEnabled = true;
                            TextBox_Mod_Folder.IsEnabled = true;
                            break;
                        case ProgramState.RUNNING:
                            Title = "Stellaris Mod Language Patcher - Running";
                            Button_LoadModList.IsEnabled = false;
                            Button_Patch.IsEnabled = false;
                            TextBox_From.IsEnabled = false;
                            TextBox_To.IsEnabled = false;
                            TextBox_Mod_Folder.IsEnabled = false;
                            break;
                        case ProgramState.ERROR:
                            Title = "Stellaris Mod Language Patcher - Error";
                            Button_LoadModList.IsEnabled = false;
                            Button_Patch.IsEnabled = false;
                            TextBox_From.IsEnabled = false;
                            TextBox_To.IsEnabled = false;
                            TextBox_Mod_Folder.IsEnabled = false;
                            break;
                    }
                });

                return true;
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            ChangeState(ProgramState.RUNNING);
            TextBox_Mod_Folder.Text = mod_folder_path;

            DataGridTextColumn_From.Header = "Support " + TextBox_From.Text;
            DataGridTextColumn_To.Header = "Support " + TextBox_To.Text;
            ChangeState(ProgramState.IDLE);
        }

        public List<Mod> LoadModList(string path, string from, string to)
        {
            DirectoryInfo di = new DirectoryInfo(path);

            var mod_folders = di.GetDirectories();

            List<Mod> mod_list = new List<Mod>();
            foreach (var mod_folder in mod_folders)
            {
                Mod mod = new Mod();
                mod.Folder = mod_folder;
                mod.Code = mod_folder.Name;

                try
                {
                    string mod_descriptor_path = System.IO.Path.Join(mod_folder.FullName, "descriptor.mod");
                    using (var stream = new StreamReader(mod_descriptor_path))
                    {
                        while (stream.Peek() >= 0)
                        {
                            string? line = stream.ReadLine();
                            string[]? splited = line?.Split("=");
                            if (splited?.Length >= 2 && splited[0].Trim() == "name")
                            {
                                mod.Name = splited[1].Trim().Trim('"');
                            }
                        }
                    }
                    var localisation_folder = new DirectoryInfo(System.IO.Path.Join(mod_folder.FullName, "localisation"));
                    var localisation_synced_folder = new DirectoryInfo(System.IO.Path.Join(mod_folder.FullName, "localisation_synced"));
                    if (!localisation_folder.Exists && !localisation_synced_folder.Exists)
                    {
                        continue;
                    }

                    var from_folder = new DirectoryInfo(System.IO.Path.Join(localisation_folder.FullName, from));
                    mod.HasFrom = from_folder.Exists;

                    var to_folder = new DirectoryInfo(System.IO.Path.Join(localisation_folder.FullName, to));
                    mod.HasTo = to_folder.Exists;

                    string l_from = "l_" + from;
                    string l_to = "l_" + to;

                    if (!mod.HasFrom || !mod.HasTo)
                    {
                        Queue<DirectoryInfo> q = new Queue<DirectoryInfo>();
                        q.Enqueue(mod_folder);
                        while (q.Count > 0)
                        {
                            var root = q.Dequeue();

                            mod.HasFrom |= root.EnumerateFiles().Any(f => f.Name.Contains(l_from));
                            mod.HasTo |= root.EnumerateFiles().Any(f => f.Name.Contains(l_to));

                            foreach (var dir in root.GetDirectories())
                            {
                                q.Enqueue(dir);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    continue;
                }

                mod_list.Add(mod);
            }

            return mod_list;
        }

        private async void Button_LoadModList_Click(object sender, RoutedEventArgs e)
        {
            if (!ChangeState(ProgramState.RUNNING, ProgramState.IDLE))
            {
                return;
            }
            await UpdateModList();
            ChangeState(ProgramState.IDLE);
        }

        private async Task UpdateModList()
        {
            try
            {
                mod_folder_path = TextBox_Mod_Folder.Text;


                string from = TextBox_From.Text;
                string to = TextBox_To.Text;

                var mod_list = await Task.Run(() =>
                {
                    return LoadModList(mod_folder_path, from, to);
                });

                DataGridTextColumn_From.Header = "Support " + TextBox_From.Text;
                DataGridTextColumn_To.Header = "Support " + TextBox_To.Text;
                Mod_List.DataContext = mod_list;
                mods = mod_list;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void CopyFolder(string src, string dist)
        {
            foreach (string dirPath in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(src, dist));
            }
            foreach (string newPath in Directory.GetFiles(src, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(src, dist), false);
            }
        }

        private async void Button_Patch_Click(object sender, RoutedEventArgs e)
        {
            if (!ChangeState(ProgramState.RUNNING, ProgramState.IDLE))
            {
                return;
            }

            try
            {
                if (mods == null)
                {
                    return;
                }

                await PatchMods(mods);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                ChangeState(ProgramState.ERROR);
                return;
            }

            await UpdateModList();
            ChangeState(ProgramState.IDLE);
            MessageBox.Show("Done");
        }

        public async Task PatchMods(List<Mod> mods)
        {
            string from = TextBox_From.Text;
            string to = TextBox_To.Text;

            string l_from = "l_" + from;
            string l_to = "l_" + to;

            await Parallel.ForEachAsync(mods, async (mod, token) =>
            {
                await Task.Run(() =>
                {
                    // 파일명에 오타 있는 경우 수정
                    {
                        Queue<DirectoryInfo> q = new Queue<DirectoryInfo>();
                        if (mod.Folder == null)
                        {
                            return;
                        }

                        q.Enqueue(mod.Folder);
                        while (q.Count > 0)
                        {
                            var root = q.Dequeue();

                            foreach (var file in root.GetFiles())
                            {
                                foreach (var typo in file_name_typo_dict.Keys)
                                {
                                    if (file.Name.Contains(typo))
                                    {
                                        string new_file_path = System.IO.Path.Join(root.FullName, file.Name.Replace(typo, file_name_typo_dict[typo]));
                                        var new_file = new FileInfo(new_file_path);

                                        if (new_file.Exists)
                                        {
                                            continue;
                                        }

                                        File.Move(file.FullName, new_file.FullName);
                                    }
                                }
                            }

                            foreach (var dir in root.GetDirectories())
                            {
                                q.Enqueue(dir);
                            }
                        }
                    }

                    // from 폴더에 있는 파일들을 to 폴더로 복사
                    List<DirectoryInfo> from_folders = new List<DirectoryInfo>();
                    List<DirectoryInfo> to_folders = new List<DirectoryInfo>();
                    {
                        Queue<DirectoryInfo> q = new Queue<DirectoryInfo>();
                        if (mod.Folder == null)
                        {
                            return;
                        }

                        q.Enqueue(mod.Folder);
                        while (q.Count > 0)
                        {
                            var root = q.Dequeue();

                            if (root.Name.Contains(from))
                            {
                                string new_folder_path = System.IO.Path.Join(root.Parent?.FullName, root.Name.Replace(from, to));
                                var new_folder = new DirectoryInfo(new_folder_path);
                                if (new_folder.Exists)
                                {
                                    continue;
                                }

                                to_folders.Add(new_folder);
                                from_folders.Add(root);

                                Debug.WriteLine(root.FullName + ", " + new_folder_path);

                                Directory.CreateDirectory(new_folder.FullName);
                                CopyFolder(root.FullName, new_folder.FullName);
                            }

                            foreach (var dir in root.GetDirectories())
                            {
                                q.Enqueue(dir);
                            }
                        }
                    }

                    // from 폴더에 있지 않은 from 파일들 to 파일로 복사
                    {
                        Queue<DirectoryInfo> q = new Queue<DirectoryInfo>();
                        if (mod.Folder == null)
                        {
                            return;
                        }

                        q.Enqueue(mod.Folder);
                        while (q.Count > 0)
                        {
                            var root = q.Dequeue();

                            if (root.Name.Contains(from) || root.Name.Contains(to))
                            {
                                continue;
                            }

                            foreach (var file in root.GetFiles())
                            {
                                if (file.Name.Contains(l_from))
                                {
                                    string new_file_path = System.IO.Path.Join(root.FullName, file.Name.Replace(from, to));
                                    var new_file = new FileInfo(new_file_path);
                                    if (new_file.Exists)
                                    {
                                        continue;
                                    }

                                    Debug.WriteLine(file.FullName + ", " + new_file.FullName);

                                    File.Copy(file.FullName, new_file.FullName);

                                    string text = File.ReadAllText(new_file.FullName);
                                    text = text.Replace(l_from, l_to);
                                    File.WriteAllText(new_file.FullName, text, new UTF8Encoding(true));
                                }
                            }

                            foreach (var dir in root.GetDirectories())
                            {
                                q.Enqueue(dir);
                            }
                        }
                    }

                    // to 폴더로 복사된 from 파일을 to 파일로 변경
                    foreach (var to_folder in to_folders)
                    {
                        foreach (var file in to_folder.GetFiles())
                        {
                            if (file.Name.Contains(l_from))
                            {
                                string new_file_path = System.IO.Path.Join(to_folder.FullName, file.Name.Replace(from, to));
                                var new_file = new FileInfo(new_file_path);
                                if (new_file.Exists)
                                {
                                    continue;
                                }

                                Debug.WriteLine(file.FullName + ", " + new_file.FullName);

                                File.Move(file.FullName, new_file.FullName);

                                string text = File.ReadAllText(new_file.FullName);
                                text = text.Replace(l_from, l_to);
                                File.WriteAllText(new_file.FullName, text, new UTF8Encoding(true));
                            }
                        }
                    }
                });
            });

            // 기존 번역 모드들을 수정함
            {
                UpdateRegacyTranslationMods(mod_folder_path, from, to);
            }

            // 한글 이름 모드를 수정함
            {
                foreach (var mod in mods)
                {
                    if (mod.Code == "2747894657")
                    {
                        string localisation_synced_path = System.IO.Path.Join(mod.Folder.FullName, "localisation_synced");
                        var localisation_synced = new DirectoryInfo(localisation_synced_path);
                        foreach (var file in localisation_synced.GetFiles())
                        {
                            bool is_first = true;
                            string file_text = File.ReadAllText(file.FullName);
                            StringReader reader = new StringReader(file_text);
                            string result = "";
                            while (true)
                            {
                                string? line = reader.ReadLine();
                                if (line == null)
                                {
                                    break;
                                }

                                if (line.Contains(":") && is_first)
                                {
                                    result += line + "\n";
                                    is_first = false;
                                    continue;
                                }

                                result += line.Replace(": ", ":0 ") + "\n";
                            }

                            File.WriteAllText(file.FullName, result, new UTF8Encoding(true));
                        }
                    }
                }
                
            }
        }

        public void UpdateRegacyTranslationMods(string path, string from, string to)
        {
            DirectoryInfo di = new DirectoryInfo(path);

            var mod_folders = di.GetDirectories();

            string l_from = "l_" + from;
            string l_to = "l_" + to;
            foreach (var mod_folder in mod_folders)
            {
                bool is_regacy = force_patch_dict.ContainsKey(to) && force_patch_dict[to].Contains(mod_folder.Name);
                if (!is_regacy)
                {
                    continue;
                }

                try
                { 
                    Queue<DirectoryInfo> q = new Queue<DirectoryInfo>();
                    q.Enqueue(mod_folder);
                    while (q.Count > 0)
                    {
                        var root = q.Dequeue();

                        foreach (var file in root.GetFiles())
                        {
                            if (file.Name.Contains(l_to))
                            {
                                string t = File.ReadAllText(file.FullName);
                                t = t.Replace(l_from, l_to);
                                File.WriteAllText(file.FullName, t, new UTF8Encoding(true));
                            }
                        }

                        foreach (var dir in root.GetDirectories())
                        {
                            q.Enqueue(dir);
                        }
                    }
                }
                catch (Exception)
                {
                    continue;
                }

                try
                {
                    var localisation_folder = new DirectoryInfo(System.IO.Path.Join(mod_folder.FullName, "localisation"));
                    if (!localisation_folder.Exists)
                    {
                        continue;
                    }

                    var korean_folder = new DirectoryInfo(System.IO.Path.Join(localisation_folder.FullName, "korean"));
                    if (!korean_folder.Exists)
                    {
                        korean_folder.Create();
                    }

                    foreach (var file in localisation_folder.GetFiles())
                    {
                        if (file.Name.Contains(l_to))
                        {
                            var new_file_path = System.IO.Path.Join(korean_folder.FullName, file.Name);
                            var new_file = new FileInfo(new_file_path);
                            if (new_file.Exists)
                            {
                                continue;
                            }

                            File.Copy(file.FullName, new_file.FullName);
                        }
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }
    }

    public class Mod
    { 
        public string? Code { get; set; }
        public DirectoryInfo? Folder { get; set; }
    
        public string? Name { get; set; }
        public string? Version { get; set; }
        public bool HasFrom { get; set; }
        public bool HasTo { get; set; }
        public bool Checked { get; set; }
    }

}
