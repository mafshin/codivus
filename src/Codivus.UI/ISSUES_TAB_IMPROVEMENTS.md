# Issues Tab Improvements

## Overview
The issues tab has been enhanced with several new features to improve the code review and issue analysis experience.

## New Features

### 1. File Tree Filtering
- The file tree sidebar now filters based on the current severity and category filters
- Only files containing issues that match the selected filters are shown
- **Toggle Option**: Users can toggle between "Show Filtered" (filtered view) and "Show All" (all files) modes

### 2. Source Code Viewer
- When a file is clicked in the tree, a source code viewer opens showing the file content with syntax highlighting
- **Issue Highlighting**: Lines with issues are highlighted with a warning background
- **Line Numbers**: Clickable line numbers with issue indicators
- **Issue Tooltips**: Hover over lines with issues to see detailed issue information

### 3. Two-Column Layout
- When a file is selected, the view switches to a two-column layout:
  - **Left**: Source code viewer with highlighted issues
  - **Right**: List of issues specific to the selected file
- **Responsive Design**: The layout adjusts based on screen size

### 4. Enhanced Navigation
- Clear file selection button to return to the full issues list
- File statistics showing total files and files with issues
- Filter indicator showing how many files match the current filters

## Implementation Details

### Components Added/Modified
1. **SourceCodeViewer.vue** (NEW): Handles file content display with syntax highlighting and issue markers
2. **IssuesTab.vue** (MODIFIED): Enhanced with file tree filtering and two-column layout
3. **FileTreeSidebar.vue** (MODIFIED): Added filter toggle and enhanced statistics
4. **ScanDetailsView.vue** (MODIFIED): Added file content fetching functionality

### Key Features
- **Syntax Highlighting**: Uses PrismJS for code highlighting
- **Language Detection**: Automatically detects file language based on extension
- **Issue Markers**: Visual indicators for lines with issues
- **Performance**: Only loads file content when needed
- **Error Handling**: Graceful handling of file loading errors

### Usage
1. Navigate to a scan's issues tab
2. Apply severity and/or category filters to narrow down issues
3. The file tree will automatically filter to show only relevant files
4. Click on any file to view its source code with highlighted issues
5. Use the toggle button to switch between filtered and all-files view
6. Click "Show all files" to return to the full issues list

### Technical Notes
- File content is fetched via the existing `/repositories/{id}/files` API endpoint
- Syntax highlighting supports 20+ programming languages
- Issue highlighting is applied at the line level based on `lineNumber` property
- The implementation is fully reactive and updates automatically when filters change
