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
        public MainWindow()
        {
            InitializeComponent();
            TextBox_Mod_Folder.Text = mod_folder_path;
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
                                mod.Name = "(" + mod.Code + ")\t" + splited[1].Trim().Trim('"');
                            }
                        }
                    }
                    var localisation_folder = new DirectoryInfo(System.IO.Path.Join(mod_folder.FullName, "localisation"));
                    if (!localisation_folder.Exists)
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

                if (mod.HasFrom && !mod.HasTo)
                {
                    mod_list.Add(mod);
                }
            }

            return mod_list;
        }

        private void Button_LoadModList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                mod_folder_path = TextBox_Mod_Folder.Text;

                string from = TextBox_From.Text;
                string to = TextBox_To.Text;

                var mod_list = LoadModList(mod_folder_path, from, to);

                DataContext = mod_list;
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
                File.Copy(newPath, newPath.Replace(src, dist), true);
            }
        }

        private void Button_Patch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (mods == null)
                {
                    return;
                }

                string from = TextBox_From.Text;
                string to = TextBox_To.Text;

                string l_from = "l_" + from;
                string l_to = "l_" + to;

                foreach (var mod in mods)
                {
                    // from 폴더를 to 폴더로 복사
                    List<DirectoryInfo> from_folders = new List<DirectoryInfo>();
                    List<DirectoryInfo> to_folders = new List<DirectoryInfo>();
                    {
                        Queue<DirectoryInfo> q = new Queue<DirectoryInfo>();
                        if (mod.Folder == null)
                        {
                            continue;
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
                            continue;
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
                                    var new_file = new DirectoryInfo(new_file_path);
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
                                var new_file = new DirectoryInfo(new_file_path);
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
                }
                MessageBox.Show("Done");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
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
