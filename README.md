# EZcfg

A simple way to lock LOL settings or upload them to the cloud? Change accounts without any worries!  
​**一个简单的方式锁定LOL的设置，或是上传到云端？无所顾忌的更换账号！​**​  

---

## 📦 项目概述 / Project Overview  
✅ ​**开箱即用**​ / Out-of-the-box  
- 提供 `MainWindow.xaml` 和 `MainWindow.xaml.cs`，​**无需修改即可直接编译客户端应用**​（仅需替换服务器地址 `/upload.php`）。  
  - Provided `MainWindow.xaml` & `MainWindow.xaml.cs` — ready to build a working client app (just change the `/upload.php` endpoint).  

- ​**附赠服务端脚本**​：`upload.php` ​**直接可用** |且附赠一套基于数据库的验证方法​。  
  - Includes server-side `upload.php` — works out of the box|Includes a complete database-based authentication solution.  

🔧 ​**功能完整但简陋**​ / Functional but Basic  
> "它们也许不那么好，但好歹功能没问题，不是吗？"  
> *"They may not be perfect, but at least they work, right?"*  

---

## 🚀 快速开始 / Quick Start  
1. ​**客户端**​：编译 XAML 项目，修改 `upload.php` 地址，或是直接在release中下载，不过不保证服务器稳定性。  
   - *Client*: Build the XAML project, then update the server URL. Alternatively, you can download directly from the release, but server stability is not guaranteed.  
2. ​**服务端**​：部署 `upload.php` 到你的服务器，替换 `APIKEY` 的实现方法（或是直接用）。  
   - *Server*: Deploy upload.php to your server and replace the APIKEY implementation(or not).  

---

## ❓ 支持 / Support  
​**小型易用项目**，如果仍有问题，也欢迎提交issues！  
*This is a very simple small project. If you encounter any issues, feel free to submit them!*  
