SHELL := /bin/bash
.DEFAULT_GOAL := help

PROJECT := src/IGoLibrary.Ex.Desktop/IGoLibrary.Ex.Desktop.csproj
TEST_PROJECT := tests/IGoLibrary.Ex.Tests/IGoLibrary.Ex.Tests.csproj
CONFIGURATION ?= Release
VERSION ?= $(shell sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' Directory.Build.props | head -n 1)
DOTNET ?= $(or $(shell command -v dotnet 2>/dev/null),$(wildcard $(HOME)/.dotnet/dotnet),dotnet)
HOST_ARCH := $(shell uname -m)
RUNTIME ?= $(if $(filter x86_64 amd64,$(HOST_ARCH)),linux-x64,$(if $(filter aarch64 arm64,$(HOST_ARCH)),linux-arm64,))
REPO ?=
RELEASE_TAG ?= linux-v$(VERSION)
RELEASE_TITLE ?= IGoLibrary-Ex Linux v$(VERSION)
RELEASE_FLAGS ?= --prerelease
RELEASE_NOTES ?= docs/linux-release-notes.md
WSL_DISTRIBUTION ?= $(WSL_DISTRO_NAME)

DOTNET_DIRECTORY := $(patsubst %/,%,$(dir $(DOTNET)))
export PATH := $(DOTNET_DIRECTORY):$(PATH)

.PHONY: help check-dotnet check-runtime check-gh check-repo check-wsl run run-debug test publish release verify windows-install windows-uninstall github-release

help:
	@echo "IGoLibrary-Ex Linux commands"
	@echo
	@echo "  make run          Build and start the app in Release mode"
	@echo "  make run-debug    Build and start the app in Debug mode"
	@echo "  make test         Run the main test suite"
	@echo "  make publish      Build the package for this machine ($(RUNTIME))"
	@echo "  make release      Build linux-x64 and linux-arm64 packages"
	@echo "  make verify       Verify both existing release packages"
	@echo "  make windows-install"
	@echo "                    Add a Windows Start menu entry for this WSLg app"
	@echo "  make windows-uninstall"
	@echo "                    Remove the Windows launcher registration"
	@echo "  make github-release REPO=owner/repository"
	@echo "                    Build and upload a GitHub prerelease"
	@echo
	@echo "Optional variables: VERSION, CONFIGURATION, RUNTIME, DOTNET, RELEASE_TAG"

check-dotnet:
	@"$(DOTNET)" --version >/dev/null 2>&1 || { \
		echo ".NET SDK was not found. Set DOTNET=/path/to/dotnet or add it to PATH." >&2; \
		exit 1; \
	}

check-runtime:
	@if [[ "$(RUNTIME)" != "linux-x64" && "$(RUNTIME)" != "linux-arm64" ]]; then \
		echo "Unsupported host architecture: $(HOST_ARCH). Set RUNTIME=linux-x64 or linux-arm64." >&2; \
		exit 1; \
	fi

check-gh:
	@command -v gh >/dev/null 2>&1 || { echo "GitHub CLI (gh) was not found." >&2; exit 1; }
	@gh auth status >/dev/null 2>&1 || { echo "GitHub CLI is not authenticated. Run: gh auth login" >&2; exit 1; }

check-repo:
	@if [[ -z "$(REPO)" ]]; then \
		echo "REPO is required. Example: make github-release REPO=owner/repository" >&2; \
		exit 1; \
	fi

check-wsl:
	@if [[ -z "$(WSL_DISTRIBUTION)" ]]; then \
		echo "This command must run inside WSL or WSL_DISTRIBUTION must be set." >&2; \
		exit 1; \
	fi
	@command -v powershell.exe >/dev/null 2>&1 || { echo "Windows PowerShell interop is unavailable." >&2; exit 1; }

run: check-dotnet
	"$(DOTNET)" run --project "$(PROJECT)" --configuration "$(CONFIGURATION)"

run-debug: check-dotnet
	"$(DOTNET)" run --project "$(PROJECT)" --configuration Debug

test: check-dotnet
	"$(DOTNET)" test "$(TEST_PROJECT)" --configuration Debug --nologo

publish: check-dotnet check-runtime
	APP_VERSION="$(VERSION)" ./build/publish-linux.sh "$(CONFIGURATION)" "$(RUNTIME)"

release: check-dotnet
	APP_VERSION="$(VERSION)" ./build/publish-linux.sh "$(CONFIGURATION)" linux-x64
	APP_VERSION="$(VERSION)" ./build/publish-linux.sh "$(CONFIGURATION)" linux-arm64

verify:
	./build/verify-linux-package.sh \
		"artifacts/linux/linux-x64/IGoLibrary-Ex-v$(VERSION)-linux-x64.tar.gz" \
		linux-x64 "$(VERSION)"
	./build/verify-linux-package.sh \
		"artifacts/linux/linux-arm64/IGoLibrary-Ex-v$(VERSION)-linux-arm64.tar.gz" \
		linux-arm64 "$(VERSION)"

windows-install: check-wsl publish
	WSL_DISTRO_NAME="$(WSL_DISTRIBUTION)" ./build/install-windows-wsl-launcher.sh \
		"$(abspath artifacts/publish/$(RUNTIME)/IGoLibrary.Ex.Desktop)" \
		"$(VERSION)"

windows-uninstall: check-wsl
	powershell.exe -NoProfile -ExecutionPolicy Bypass \
		-File "$$(wslpath -w build/install-windows-wsl-launcher.ps1)" \
		-Uninstall

github-release: check-gh check-repo release
	gh release create "$(RELEASE_TAG)" \
		"artifacts/linux/linux-x64/IGoLibrary-Ex-v$(VERSION)-linux-x64.tar.gz" \
		"artifacts/linux/linux-x64/IGoLibrary-Ex-v$(VERSION)-linux-x64.tar.gz.sha256" \
		"artifacts/linux/linux-arm64/IGoLibrary-Ex-v$(VERSION)-linux-arm64.tar.gz" \
		"artifacts/linux/linux-arm64/IGoLibrary-Ex-v$(VERSION)-linux-arm64.tar.gz.sha256" \
		--repo "$(REPO)" \
		--target "$$(git rev-parse HEAD)" \
		--title "$(RELEASE_TITLE)" \
		--notes-file "$(RELEASE_NOTES)" \
		$(RELEASE_FLAGS)
