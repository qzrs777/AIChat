#!/bin/bash

# ================= 配置 =================
WORKDIR="/home/joeodagiri/AIChat/SoVITS_V2_update/"  #PATH to your GPT-SoVITS-v2pro folder
LOG_FILE="$WORKDIR/gpt_api_debug.log"
# =======================================

# 1. 准备环境
source /home/joe/miniconda3/etc/profile.d/conda.sh
conda activate GPTSoVits
# 找到 cuDNN 路径并添加到环境变量
CUDNN_PATH=$(dirname $(python -c "import nvidia.cudnn; print(nvidia.cudnn.__file__)"))/lib
export LD_LIBRARY_PATH=$LD_LIBRARY_PATH:$CUDNN_PATH
cd "$WORKDIR"

# 2. 启动 Python API
nohup /home/joe/miniconda3/envs/GPTSoVits/bin/python api_v2_ex.py -a 127.0.0.1 -p 9880 > "$LOG_FILE" 2>&1 &
API_PID=$!
echo "API Started. PID: $API_PID" >> "$LOG_FILE"
