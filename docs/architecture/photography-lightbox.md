# Photography Lightbox

**Version:** 1.0  
**Status:** Approved  
**Last Updated:** July 31, 2026

## Purpose

The Photography Lightbox provides an immersive full-screen viewing experience for destination photographs.

It should elevate photography without interrupting the editorial reading experience.

## Requirements

- Open from any destination gallery image
- Display the selected image at the largest practical size
- Show caption and image position
- Previous and next navigation
- Close button
- Escape key closes
- Left and right arrow keys navigate
- Background click closes
- Keyboard-accessible controls
- Focus remains within the lightbox while open
- Return focus to the originating image after closing
- Prevent background scrolling
- Support touch-friendly controls
- Respect reduced-motion preferences

## Initial Scope

Version 1 will support destination gallery images only.

Future versions may support:

- story images
- homepage photography
- slideshows
- swipe gestures
- image metadata
- download permissions
- panorama viewing

## Component Design

```text
DestinationGallery
    ↓
PhotographyLightbox