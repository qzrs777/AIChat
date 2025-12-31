GPT-SoVITS-linux 环境配置:

conda create -n GPTSoVits python=3.11
conda activate GPTSoVits
pip install torch==2.7.0 torchvision==0.22.0 torchaudio==2.7.0 --index-url https://download.pytorch.org/whl/cu128
pip install opencc-python-reimplemented

## 删掉 GPT-SoVITS/requirements.txt文件中第一行的 "--no-binary=opencc"

pip install -r extra-req.txt --no-deps
pip install -r requirements.txt
conda install ffmpeg

## 在archlinux系统上测试时, 补安装了sox和opencc

## 这里好像也可以都升级到最新, 不会报warning
pip install peft==0.7.1 transformers==4.36.2

## 原本windows环境升级上只装这两个应该就够了
pip install python-multipart faster-whisper


## 对于使用StartAI_linux.bat从游戏内直接打开进程的设置, 在游戏内要写成"Z:/绝对路径到StartAI_linux.bat这个文件的形式"(例如"Z:/home/joe/AIChat/SoVITS_V2_update/StartAI_linux.bat"), 同时StartAI_linux.bat和run_gpt.sh这两个文件中的地址也需要对应修改.

## 且由于steam的proton的沙盒环境机制, 现在没有找到方法可以在游戏界面内结束游戏后自动回收外部python子进程, 需要在steam库界面再手动点击一次停止程序后才会关闭所有进程. 
