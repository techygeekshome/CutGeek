<div align="center">

<img src="icons/cutgeek-256.png" alt="CutGeek logo" width="96" height="96">

# CutGeek

**Cut the background out of a photo at full resolution, on your own machine.**

[![Build](https://github.com/techygeekshome/CutGeek/actions/workflows/build.yml/badge.svg)](https://github.com/techygeekshome/CutGeek/actions/workflows/build.yml)
[![Version](https://img.shields.io/github/v/release/techygeekshome/CutGeek?label=version&color=4c9bff)](https://github.com/techygeekshome/CutGeek/releases)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078d4)](#download)
[![License](https://img.shields.io/badge/License-GPL--3.0-blue)](LICENSE)
[![Made by TechyGeeksHome](https://img.shields.io/badge/made%20by-TechyGeeksHome-b191f2)](https://techygeekshome.info)
[![Support on Ko-fi](https://img.shields.io/badge/support-Ko--fi-ff5e5b)](https://ko-fi.com/techygeekshome)

[Download](#download) · [What it does](#what-it-does) · [The models](#the-models) · [Requirements](#requirements)

</div>

---

Drop a photograph in and CutGeek writes out a copy with the background removed - transparent, a
flat colour, or the original blurred behind the subject. It runs the U²-Net and IS-Net models
locally through the ONNX runtime. No account, no server, no upload, no credits.

Part of the [TechyGeeksHome](https://techygeekshome.info/geek-tools/) range.

---

## Download

**[Download the latest release](https://github.com/techygeekshome/CutGeek/releases/latest)**, or read about it on the
**[CutGeek product page](https://techygeekshome.info/cutgeek/)**.

Windows 10 or 11, 64-bit. Nothing else to install.

---

## What it does

- Removes the background from JPEG, PNG, WebP, BMP, GIF and HEIC images
- Writes the result at **exactly the size of the photograph that went in**
- Transparent PNG, a flat colour, or the original background blurred
- Queues as many images as you like and works through them one at a time
- Reads EXIF orientation, so phone photos come out the right way up

---

## What it will not do

- **It does not upload your photographs.** The model runs in this process, on your own
  processor. The only thing the app ever downloads is a model, from the Models screen, when you
  ask for one.
- **It does not shrink the result or watermark it.** That is the thing the web services charge
  for, and there is nothing to charge for - the model only ever looks at a small square, so the
  resolution of the output never cost anything to begin with.
- **It does not change your originals.** The photograph is only ever read. If a cutout of that
  name is already there, the next one is numbered.
- **It does not count anything.** No credits, no monthly allowance, no per-image limit, no trial.

---

## Models

Nothing is included in the installer. Models are fetched on first use and kept in
`%LocalAppData%\TechyGeeksHome\CutGeek\models`. **Every download is checked against a pinned
SHA-256 and deleted if it does not match**, so CutGeek runs the exact file it was tested with or
it runs nothing at all.

| Model | Download | When to use it |
|---|---|---|
| Quick | 4.6 MB | A clear subject on a plain background |
| Standard | 176 MB | The usual choice - start here |
| People | 176 MB | Portraits, and only portraits |
| Detailed | 179 MB | Hair and thin edges. Reads at 1024 px instead of 320. Slower |

The weights are the ONNX builds published by [rembg](https://github.com/danielgatis/rembg), of
[U²-Net](https://github.com/xuebinqin/U-2-Net) and [IS-Net](https://github.com/xuebinqin/DIS),
both Apache-2.0.

---

## Requirements

Windows 10 1809 or later, 64-bit. .NET 8 is included in the installer build. CPU only - there is
no GPU package, because a GPU build would pull in several hundred megabytes of CUDA that most
people cannot use.

---

## Building

```
dotnet build CutGeek.sln -c Release
```

The end-to-end check needs a model and an image:

```
CG_TEST_MODEL=path\to\u2net.onnx CG_TEST_IMAGE=path\to\photo.jpg dotnet run --project tests\CutGeek.Tests
```

---

## Licence

GPL-3.0. Free to use, including at work. No paid tier, ever.
