# -*- coding: utf-8 -*-
import sys
import io
import argparse
import yt_dlp
import subprocess
import time
import os
import traceback

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace')

def start_bypass(bypass_bat_path):
    if not bypass_bat_path or not os.path.exists(bypass_bat_path):
        print("Bypass not found, skipping.")
        return None
    print("Starting bypass...")
    try:
        bypass_process = subprocess.Popen(
            [bypass_bat_path],
            shell=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            creationflags=subprocess.CREATE_NO_WINDOW
        )
        time.sleep(20)
        print("Bypass should be active.")
        return bypass_process
    except Exception as e:
        print(f"Failed to start bypass: {e}")
        traceback.print_exc()
        return None

def main():
    parser = argparse.ArgumentParser(description='YouTube downloader')
    parser.add_argument('--url', required=True, help='Video URL')
    parser.add_argument('--quality', default='bestvideo+bestaudio/best', help='Quality format')
    parser.add_argument('--output-format', default='mp4', help='Container format')
    parser.add_argument('--output-dir', default='./Downloads', help='Output directory')
    parser.add_argument('--cookies', help='Path to cookies.txt')
    parser.add_argument('--bypass-bat', help='Path to bypass .bat file')
    args = parser.parse_args()

    print(f"yt-dlp version: {yt_dlp.version.__version__}")
    
    os.makedirs(args.output_dir, exist_ok=True)

    bypass_proc = start_bypass(args.bypass_bat)

    ydl_opts = {
    'outtmpl': f'{args.output_dir}/%(title)s.%(ext)s',
    'format': 'bestvideo[ext=mp4][vcodec^=avc1]+bestaudio[ext=m4a]/best[ext=mp4]',
    'merge_output_format': 'mp4',
    'noplaylist': True,
    'socket_timeout': 60,
    'retries': 20,
    'geo_bypass': True,
    'quiet': False,
    'verbose': True,
    'cookiefile': args.cookies,
}

    if args.cookies and os.path.exists(args.cookies):
        ydl_opts['cookiefile'] = args.cookies
        print(f"Using cookies from {args.cookies}")
    else:
        print("No cookies file used.")

    # impersonate временно отключён из-за AssertionError в nightly yt-dlp
    # Если нужно, можно включить позже, но для работы и так должно хватить
    print("Impersonate disabled to avoid AssertionError. Downloading without it.")
    
    print(f"Starting download: {args.url}")
    try:
        with yt_dlp.YoutubeDL(ydl_opts) as ydl:
            info = ydl.extract_info(args.url, download=False)
            print(f"Video title: {info.get('title', 'Unknown')}")
            print(f"Available formats: {len(info.get('formats', []))}")
            ydl.download([args.url])
        print("\nDownload completed successfully.")
        sys.exit(0)
    except Exception as e:
        print(f"\nERROR: {e}")
        traceback.print_exc()
        print("\n--- Hints ---")
        print("1. Update yt-dlp: pip install --upgrade --pre yt-dlp")
        print("2. Try lower quality: --quality 18")
        print("3. Check bypass tool is running")
        sys.exit(1)
    finally:
        if bypass_proc:
            bypass_proc.terminate()
            print("Bypass stopped.")

if __name__ == "__main__":
    main()