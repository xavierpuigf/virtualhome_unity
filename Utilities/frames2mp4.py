import os
import subprocess
import argparse
import shutil

try:
  from subprocess import DEVNULL # py3k
except ImportError:
  DEVNULL = open(os.devnull, 'wb')

# Change these directories as needed
EXT_MP4 = '.mp4'
EXT_TXT = '.txt'
EXT_MAT = '.mat'

FRAMERATE = str(5)
CRF_NORMAL = str(20)
CRF_GT = str(0)
TUNE_ANIMATION = 'animation'
TUNE_FILM = 'film'

FRAME_TYPES = ['_depth', '_flow', '_normal', '_seg_inst', '_seg_class']
CRF_VALUES = [CRF_GT, CRF_GT, CRF_NORMAL, CRF_GT, CRF_GT]
TUNINGS = [TUNE_FILM, TUNE_FILM, TUNE_FILM, TUNE_ANIMATION, TUNE_ANIMATION]

def removePngFiles(path):
  for item in os.listdir(path):
    if '.png' in item:
      os.remove(os.path.join(path, item))

def moveTxtFiles(path_gt, path_out):
  for item in os.listdir(path_gt):
    if '.txt' in item:
      origPath = os.path.join(path_gt, item)
      newPath = os.path.join(path_out, item)
      os.rename(origPath, newPath)

def createMP4(path_gt, path_out, scriptName, framerate, isRGB):
  # For more options of H.264 encoding, visit
  # https://trac.ffmpeg.org/wiki/Encode/H.264

  if isRGB:
    cmd = ['ffmpeg',
      '-framerate', str(framerate),
      '-i', '{}/Action_%04d.png'.format(path_gt),
      '-crf', str(CRF_NORMAL),
      '-tune', str(TUNE_FILM),
      '{}/{}.mp4'.format(path_out, scriptName)
    ]

    subprocess.call(cmd, stderr=DEVNULL)

  else:
    for i in range( len(FRAME_TYPES) ):
      cmd = ['ffmpeg',
        '-framerate', str(framerate),
        '-i', '{}/Action_%04d{}.png'.format(path_gt, FRAME_TYPES[i]),
        '-crf', str(CRF_VALUES[i]),
        '-tune', str(TUNINGS[i]),
        '{}/{}{}.mp4'.format(path_out, scriptName, FRAME_TYPES[i])
      ]

      subprocess.call(cmd, stderr=DEVNULL)

def moveScriptFiles(dir_in, scriptName, dir_out):
  fName = scriptName + EXT_TXT
  origPath = os.path.join(dir_in, fName)
  newPath = os.path.join(dir_out, fName)
  print('From:' + origPath)
  print('To:' + newPath)
  shutil.move(origPath, newPath)

  # Move .mat file if there is any
  fName = scriptName + EXT_MAT
  origPath = os.path.join(dir_in, fName)
  if os.path.exists(origPath):
    newPath = os.path.join(dir_out, fName)
    print('From:' + origPath)
    print('To:' + newPath)
    shutil.move(origPath, newPath)


# ============================ Main code ============================ #
def main():
  parser = argparse.ArgumentParser()

  parser.add_argument('script_name', help='script name')
  parser.add_argument('dir_in_gt', help='Directory ground truth are stored. Actual gt would be stored on: <dir_in_gt>/<script_name>')
  parser.add_argument('dir_in_script', help='Directory where scripts are stored')
  parser.add_argument('dir_out', help='Directory where renderings and scripts will be stored')
  parser.add_argument('framerate')
  parser.add_argument('--keep_frames', help='If set, raw frames will be intact', action='store_true')
  parser.add_argument('--keep_scripts', help='If set, done scripts will not be moved from the original location', action='store_true')
  parser.add_argument('--rgb_only', help='=Use this mode if frames are rgb. Frame type suffix won\'t be used', action='store_true')

  args = parser.parse_args()

  dir_gt = os.path.join(args.dir_in_gt, args.script_name)

  dir_out_rdr = os.path.join(args.dir_out, args.script_name)
  dir_out_script = os.path.join(args.dir_out, 'scripts')

  if not os.path.exists(dir_out_rdr):
    os.makedirs(dir_out_rdr)
  if not os.path.exists(dir_out_script):
    os.makedirs(dir_out_script)

  if not args.keep_scripts:
    moveScriptFiles(args.dir_in_script, args.script_name, dir_out_script)
  createMP4(dir_gt, dir_out_rdr, args.script_name, args.framerate, args.rgb_only)

  moveTxtFiles(dir_gt, dir_out_rdr)
  if not args.keep_frames:
    removePngFiles(dir_gt)

if __name__ == "__main__":
  main()
