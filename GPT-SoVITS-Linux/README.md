# GPT-SoVITS 在 Linux 下的部署方法

将 GPT-SoVITS 克隆到本地，在其根目录下进行如下操作：
```bash
conda create -n GPTSoVits python=3.11
conda activate GPTSoVits
pip install torch==2.7.0 torchvision==0.22.0 torchaudio==2.7.0 --index-url https://download.pytorch.org/whl/cu128
pip install opencc-python-reimplemented
```
手动删掉 `GPT-SoVITS/requirements.txt` 文件中第一行的 `"--no-binary=opencc"`。

```bash
pip install -r extra-req.txt --no-deps
pip install -r requirements.txt
conda install ffmpeg
```

注：
- 在 Arch Linux 上测试时，补安装了 `sox` 和 `opencc` 两个包。

```bash
## 这里好像也可以都升级到最新, 不会报warning
pip install peft==0.7.1 transformers==4.36.2

## 原本windows环境升级上只装这两个应该就够了
pip install python-multipart faster-whisper
```

## 注意
对于使用 `StartAI_linux.bat` 从游戏内直接打开进程的设置：
- 在游戏内要写成 `"Z:/<StartAI_linux.bat的绝对路径>` 形式，例如 `"Z:/home/joe/AIChat/SoVITS_V2_update/StartAI_linux.bat"`。
- 同时 `StartAI_linux.bat` 和 `run_gpt.sh` 这两个文件中的地址也需要对应修改.

- 由于 steam 的 proton 的沙盒环境机制, 现在没有找到方法可以在游戏界面内结束游戏后自动回收外部 python 子进程, 需要在 steam 库界面再手动点击一次停止程序后才会关闭所有进程。
  > 更新：其实访问 `http://127.0.0.1:9880/control?command=exit` 就行了。
