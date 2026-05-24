"""
Tail training.log and print one-line alerts when interesting things happen.

Run this in a separate terminal while training runs in the background.
It will scream into stdout on:
  * stage advance (a "Parameter '...' is in lesson 'S..._...'" line)
  * reward collapse (mean reward drops > 0.5 vs the recent baseline)
  * "Saved model" lines (so we know when .pt files hit disk)
  * the trainer process ending (no new lines for >2 minutes)

Usage:
    python C:\\VR\\scripts\\watch_training.py
    python C:\\VR\\scripts\\watch_training.py --log C:\\VR\\training.log
"""

import argparse
import re
import time
from collections import deque
from pathlib import Path

STEP_RE = re.compile(
    r"Step:\s*(\d+)\.\s*Time Elapsed:\s*([\d.]+)\s*s\.\s*Mean Reward:\s*(-?[\d.]+)"
)
LESSON_RE = re.compile(r"is in lesson '([^']+)'")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--log", default=r"C:\VR\training.log")
    ap.add_argument("--collapse-drop", type=float, default=0.5,
                    help="Alert if mean reward drops this much vs trailing avg")
    ap.add_argument("--idle-secs", type=float, default=120.0,
                    help="Alert if no new line for this long")
    args = ap.parse_args()

    log = Path(args.log)
    print(f"[watch] tailing {log}")

    last_size = 0
    last_change = time.time()
    recent_rewards = deque(maxlen=10)
    current_lesson = None
    last_step = None

    while True:
        try:
            size = log.stat().st_size
        except FileNotFoundError:
            time.sleep(2)
            continue

        if size > last_size:
            with log.open("r", encoding="utf-8", errors="replace") as f:
                f.seek(last_size)
                chunk = f.read()
            last_size = size
            last_change = time.time()

            for line in chunk.splitlines():
                m = STEP_RE.search(line)
                if m:
                    step = int(m.group(1))
                    reward = float(m.group(3))
                    last_step = step
                    if recent_rewards:
                        avg = sum(recent_rewards) / len(recent_rewards)
                        if reward < avg - args.collapse_drop:
                            print(f"⚠  REWARD DROP at step {step}: {reward:.3f} "
                                  f"(was avg {avg:.3f})")
                    recent_rewards.append(reward)
                    continue

                lm = LESSON_RE.search(line)
                if lm and lm.group(1) != current_lesson:
                    if current_lesson is not None:
                        print(f"🎉  STAGE ADVANCE → {lm.group(1)}  "
                              f"(at step ~{last_step})")
                    current_lesson = lm.group(1)
                    recent_rewards.clear()
                    continue

                if "Saved model" in line or "Exported" in line:
                    print(f"💾  {line.strip()}")
                if "Learning was interrupted" in line or "Traceback" in line:
                    print(f"❌  {line.strip()}")

        elif size < last_size:
            # log rotated/truncated
            last_size = 0
            recent_rewards.clear()
            print("[watch] log truncated — resetting cursor")
        else:
            idle = time.time() - last_change
            if idle > args.idle_secs:
                print(f"⏸  no new log lines for {idle:.0f}s — trainer may be "
                      f"stuck or finished. last step: {last_step}")
                last_change = time.time()  # re-arm

        time.sleep(2)


if __name__ == "__main__":
    main()
