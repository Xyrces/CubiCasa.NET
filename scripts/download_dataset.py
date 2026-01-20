import os
import requests
import zipfile
from tqdm import tqdm

DATA_URL = "https://zenodo.org/records/2613548/files/cubicasa5k.zip?download=1"
DEST_FOLDER = "data"
ZIP_FILE = os.path.join(DEST_FOLDER, "cubicasa5k.zip")

def download_file(url, filename):
    response = requests.get(url, stream=True)
    total_size = int(response.headers.get('content-length', 0))
    block_size = 1024 # 1 Kibibyte

    with open(filename, 'wb') as file, tqdm(
        desc=filename,
        total=total_size,
        unit='iB',
        unit_scale=True,
        unit_divisor=1024,
    ) as bar:
        for data in response.iter_content(block_size):
            size = file.write(data)
            bar.update(size)

def main():
    if not os.path.exists(DEST_FOLDER):
        os.makedirs(DEST_FOLDER)
        print(f"Created folder: {DEST_FOLDER}")

    if os.path.exists(ZIP_FILE):
        print(f"File {ZIP_FILE} already exists. Skipping download.")
    else:
        print(f"Downloading CubiCasa5k dataset to {ZIP_FILE}...")
        download_file(DATA_URL, ZIP_FILE)
        print("Download complete.")

    # Extract
    print(f"Extracting {ZIP_FILE}...")
    with zipfile.ZipFile(ZIP_FILE, 'r') as zip_ref:
        zip_ref.extractall(DEST_FOLDER)
    print("Extraction complete.")

if __name__ == "__main__":
    main()
