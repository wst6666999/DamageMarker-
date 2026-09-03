import pytesseract
from PIL import Image
import json
import re
import sys
import os
import cv2
from pytesseract import Output

def extract_gct8c_info(text):
    """
    从OCR文本中提取GCT-8C/11机型的信息
    """
    result = {}
    
    # 提取回放员 - 优化版本，处理空格和引号问题
    if "未登录" in text:
        result['回放员'] = "未登录"
    else:
        # 模式1: 完整匹配"回放员"，允许中间有空格，后面可以跟冒号、空格
        operator_match = re.search(r'回\s*放\s*员\s*[:：\s]*([^工(串)号\n,"\']+)', text)
        # 模式2: 匹配可能的OCR错误"放员"
        if not operator_match:
            operator_match = re.search(r'放\s*员\s*[:：\s]*([^工(串)号\n,"\']+)', text)
        # 模式3: 匹配可能的OCR错误"回员"
        if not operator_match:
            operator_match = re.search(r'回\s*员\s*[:：\s]*([^工(串)号\n,"\']+)', text)
        # 模式4: 更宽松的匹配，不严格限制后面的内容
        if not operator_match:
            operator_match = re.search(r'回\s*放\s*员[:：]?\s*([^\n,"\']+)', text)
        
        if operator_match:
            operator = operator_match.group(1).strip()
            # 处理名字中间的多余空格（保留一个空格）
            operator = re.sub(r'\s+', ' ', operator)
            # 移除可能的尾部符号（包括引号）
            operator = re.sub(r'[,"\'\s:：]+$', '', operator)
            result['回放员'] = operator if operator else ""
        else:
            # 最后的尝试：查找包含"员"字且前面有少量字符的情况
            final_match = re.search(r'(.{1,3})员[:：]?\s*([^\n,"\']+)', text)
            if final_match:
                operator = f"{final_match.group(1)}员{final_match.group(2)}".strip()
                # 同样处理空格和引号
                operator = re.sub(r'\s+', ' ', operator)
                operator = re.sub(r'[,"\'\s:：]+$', '', operator)
                result['回放员'] = operator
            else:
                result['回放员'] = ""

    result['串号'] = ""

    # 调试：打印所有括号内的内容
    print("=== 串号匹配调试 ===")
    all_brackets = re.findall(r'\(([^)]+)\)', text)
    print(f"所有括号内容: {all_brackets}")
    
    # 方法1：优先匹配"工号"或"定号"后面的括号数字
    # 匹配格式：工号/定号 : 数字(数字 左/右)
    serial_match = re.search(r'(?:工\s*号|定\s*号)\s*[:：]\s*(\d+)\s*\(\s*(\d+)\s*(?:左|右)?\s*\)', text)
    if serial_match:
        result['串号'] = serial_match.group(2)  # 第二个括号中的数字
        print(f"模式1匹配成功: {result['串号']}")
    else:
        # 方法2：匹配数字(数字 左/右) 格式，且前面有冒号的
        serial_match2 = re.search(r'[:：]\s*(\d+)\s*\(\s*(\d+)\s*(?:左|右)?\s*\)', text)
        if serial_match2:
            result['串号'] = serial_match2.group(2)
            print(f"模式2匹配成功: {result['串号']}")
        else:
            # 方法3：直接匹配第一个出现的 数字(数字 左/右) 格式
            serial_match3 = re.search(r'(\d+)\s*\(\s*(\d+)\s*(?:左|右)?\s*\)', text)
            if serial_match3:
                result['串号'] = serial_match3.group(2)
                print(f"模式3匹配成功: {result['串号']}")
            else:
                # 方法4：匹配所有括号内的数字，但排除明显不是串号的
                all_bracket_nums = re.findall(r'\((\d+)\s*(?:左|右)?\)', text)
                if all_bracket_nums:
                    # 排除股号、铁号等明显不是串号的数字
                    valid_serials = []
                    for num in all_bracket_nums:
                        # 串号通常是4-5位数字，排除3位及以下的数字
                        if len(num) >= 4:
                            valid_serials.append(num)
                    
                    if valid_serials:
                        result['串号'] = valid_serials[0]  # 取第一个有效的
                        print(f"模式4匹配成功: {result['串号']}")
                    else:
                        # 如果没有4位以上的数字，取第一个
                        result['串号'] = all_bracket_nums[0]
                        print(f"模式4备选匹配: {result['串号']}")
                else:
                    # 方法5：匹配第一个数字序列
                    first_number = re.search(r'\d+', text)
                    if first_number:
                        result['串号'] = first_number.group()
                        print(f"兜底模式匹配成功: {result['串号']}")
                    else:
                        print("未匹配到任何串号")

    # 提取探伤日期
    date_match = re.search(r'探伤日期\s*,?\s*(\d{4}-\d{2}-\d{2})', text)
    if date_match:
        result['探伤日期'] = date_match.group(1)
    else:
        date_match_alt = re.search(r'(\d{4}-\d{2}-\d{2})', text)
        if date_match_alt:
            result['探伤日期'] = date_match_alt.group(1)
        else:
            result['探伤日期'] = ""

    # 提取机型 - 修正为GCT-8C/11
    print("=== 机型匹配调试 ===")
    
    # 优先匹配GCT-8C/11格式
    model_match = re.search(r'机型\s*:\s*(GCT-8C/11)', text, re.IGNORECASE)
    if model_match:
        print(f"机型匹配成功: {model_match.group(1)}")
        result['机型'] = model_match.group(1)
    else:
        # 尝试其他可能的格式
        model_match_alt = re.search(r'机型\s*:\s*([A-Z]{2,}-[A-Z0-9]+/[A-Z0-9]+)', text)
        if model_match_alt:
            print(f"备选机型匹配: {model_match_alt.group(1)}")
            result['机型'] = model_match_alt.group(1)
        else:
            # 搜索包含"机型"的行
            lines = text.split('\n')
            for line in lines:
                if '机型' in line:
                    print(f"找到包含机型的行: {line}")
                    # 尝试提取机型信息
                    model_value = re.search(r'([A-Z]{2,}-[A-Z0-9]+/[A-Z0-9]+)', line)
                    if model_value:
                        print(f"行内匹配成功: {model_value.group(1)}")
                        result['机型'] = model_value.group(1)
                        break
                    else:
                        # 提取冒号后的内容
                        parts = line.split('机型')
                        if len(parts) > 1:
                            after_model = parts[1]
                            value_match = re.search(r'[:\s]*([^\s,]+)', after_model)
                            if value_match:
                                print(f"冒号后匹配: {value_match.group(1)}")
                                result['机型'] = value_match.group(1)
                                break
    
    # 如果仍未找到，使用默认值
    if '机型' not in result or not result['机型']:
        result['机型'] = "GCT-8C/11"  # 默认值
    
    print("=== GCT-8C调试结束 ===")
    return result

def extract_jgt6m_info(text):
    """
    从OCR文本中提取JGT-6M机型的信息
    """
    result = {}
    
    # 创建去除空格的文本版本用于匹配
    text_no_spaces = re.sub(r'\s+', '', text)
    
    # 回放员 - 优化版本：如果后面是-、--、——则为空，否则提取到下一个"回"之前
    replay_found = False
    
    # 查找所有可能的回放员位置
    replay_positions = []
    
    # 标准格式匹配
    replay_patterns = [
        r"回放员[:：]?\s*([^回\n]*)",  # 匹配到下一个"回"之前
        r"放员[:：]?\s*([^回\n]*)",    # 缺少"回"字
        r"回员[:：]?\s*([^回\n]*)",    # 缺少"放"字
    ]
    
    for pattern in replay_patterns:
        matches = list(re.finditer(pattern, text))
        replay_positions.extend(matches)
    
    # 在去除空格的文本中查找
    for pattern in replay_patterns:
        pattern_no_space = pattern.replace(r'\s*', '')
        matches = list(re.finditer(pattern_no_space, text_no_spaces))
        replay_positions.extend(matches)
    
    # 处理找到的所有匹配
    for match in replay_positions:
        operator = match.group(1).strip()
        
        # 检查是否为空值（-、--、——等）
        if operator and not re.match(r'^[-—~]*$', operator):
            # 提取到下一个"回"之前的内容
            # 如果operator已经包含了下一个"回"，则截取到第一个"回"之前
            if '回' in operator:
                operator = operator.split('回')[0]
            
            # 清理操作符
            operator = re.sub(r'[\s:：,，.。\-—~]+$', '', operator)
            
            if operator:
                result["回放员"] = operator
                replay_found = True
                print(f"回放员匹配成功: {operator}")
                break
    
    if not replay_found:
        result["回放员"] = ""
        print("回放员为空或未匹配到有效回放员")

    # 串号 - 去除空格影响，支持"器编号"和"编号"等不完整匹配
    machine_patterns = [
        r"机器编号[:：]?\s*(\d+)",     # 标准格式
        r"器编号[:：]?\s*(\d+)",       # 缺少"机"字
        r"编号[:：]?\s*(\d+)",         # 缺少"机器"二字
        r"机编[:：]?\s*(\d+)",         # 缺少"号"字
        r"串号[:：]?\s*(\d+)",         # 直接匹配串号
    ]
    
    machine_found = False
    for pattern in machine_patterns:
        match = re.search(pattern, text)
        if match:
            result["串号"] = match.group(1)
            machine_found = True
            print(f"串号匹配成功(模式{machine_patterns.index(pattern)+1}): {match.group(1)}")
            break
    
    # 如果在原始文本中没找到，尝试在去除空格的文本中匹配
    if not machine_found:
        for pattern in machine_patterns:
            # 将模式中的空格去掉
            pattern_no_space = pattern.replace(r'\s*', '')
            match = re.search(pattern_no_space, text_no_spaces)
            if match:
                result["串号"] = match.group(1)
                machine_found = True
                print(f"串号无空格匹配成功(模式{machine_patterns.index(pattern)+1}): {match.group(1)}")
                break
    
    # 如果所有模式都失败，尝试直接查找4-6位数字
    if not machine_found:
        all_numbers = re.findall(r'\b\d{4,6}\b', text)
        if all_numbers:
            # 优先选择看起来像编号的数字（排除年份等）
            for num in all_numbers:
                if not (num.startswith('202') or num.startswith('201')):  # 排除年份
                    result['串号'] = num
                    machine_found = True
                    print(f"直接数字匹配串号: {num}")
                    break
            # 如果没找到合适的，用第一个
            if not machine_found and all_numbers:
                result['串号'] = all_numbers[0]
                print(f"使用第一个数字作为串号: {all_numbers[0]}")
    
    if not machine_found:
        result["串号"] = ""
        print("未匹配到串号")

    # 探伤日期 - 去除空格影响
    time_patterns = [
        r"探伤日期[:：]?\s*(\d{4}[-./]\d{2}[-./]\d{2})",
        r"探伤[:：]?\s*(\d{4}[-./]\d{2}[-./]\d{2})",  # 缺少"日期"二字
        r"日期[:：]?\s*(\d{4}[-./]\d{2}[-./]\d{2})",  # 缺少"探伤"二字
    ]
    
    time_found = False
    for pattern in time_patterns:
        match = re.search(pattern, text)
        if match:
            result["探伤日期"] = match.group(1).replace('.', '-').replace('/', '-')
            time_found = True
            print(f"探伤日期匹配成功(模式{time_patterns.index(pattern)+1}): {result['探伤日期']}")
            break
    
    # 如果在原始文本中没找到，尝试在去除空格的文本中匹配
    if not time_found:
        for pattern in time_patterns:
            # 将模式中的空格去掉
            pattern_no_space = pattern.replace(r'\s*', '')
            match = re.search(pattern_no_space, text_no_spaces)
            if match:
                result["探伤日期"] = match.group(1).replace('.', '-').replace('/', '-')
                time_found = True
                print(f"探伤日期无空格匹配成功(模式{time_patterns.index(pattern)+1}): {result['探伤日期']}")
                break
    
    # 如果没有匹配到特定格式，查找日期格式
    if not time_found:
        date_match = re.search(r'(\d{4}-\d{2}-\d{2})', text)
        if date_match:
            result["探伤日期"] = date_match.group(1)
            print(f"通用日期匹配: {result['探伤日期']}")
        else:
            result["探伤日期"] = ""
            print("未匹配到探伤日期")

    # 机型
    if "JGT-6M" in text or "JGT-6M" in text_no_spaces:
        result["机型"] = "JGT-6M"
    else:
        result["机型"] = "JGT-6M"  # 默认值
    
    print("=== JGT-6M调试结束 ===")
    return result

def extract_other_model_info(text):
    """
    从OCR文本中提取其他机型的信息 - 模糊匹配
    """
    result = {}
    
    # 创建去除空格的文本版本用于匹配
    text_no_spaces = re.sub(r'\s+', '', text)
    
    # 回放员 - 多种匹配模式，去除空格影响
    operator_patterns = [
        r'回\s*放\s*员\s*[:：\s]*([^\n]+?)(?=\s*(?:机器编号|机型|探伤|$))',
        r'回\s*放\s*员\s*[:：\s]*([^\n]+)',
        r'放\s*员\s*[:：\s]*([^\n]+)',
        r'回\s*员\s*[:：\s]*([^\n]+)'
    ]
    
    operator_found = False
    for pattern in operator_patterns:
        match = re.search(pattern, text)
        if match:
            operator = match.group(1).strip()
            operator = re.sub(r'[\s:：,，.。]+$', '', operator)
            if operator and len(operator) > 0:
                result['回放员'] = operator
                operator_found = True
                break
    
    # 如果在原始文本中没找到，尝试在去除空格的文本中查找
    if not operator_found:
        operator_no_space = re.search(r'回放员[:：]?([A-Za-z\u4e00-\u9fa5]+)', text_no_spaces)
        if operator_no_space:
            result['回放员'] = operator_no_space.group(1)
        else:
            result['回放员'] = ""

    # 串号 - 多种匹配模式，去除空格影响
    machine_patterns = [
        r'机器编号\s*[:：\s]*(\d{4,6})',
        r'器编号\s*[:：\s]*(\d{4,6})',  # 支持"器编号"
        r'编号\s*[:：\s]*(\d{4,6})',    # 支持"编号"
        r'机\s*编\s*[:：\s]*(\d{4,6})',
        r'串号\s*[:：\s]*(\d{4,6})'     # 直接匹配串号
    ]
    
    machine_found = False
    for pattern in machine_patterns:
        match = re.search(pattern, text)
        if match:
            result['串号'] = match.group(1)
            machine_found = True
            break
    
    # 如果在原始文本中没找到，尝试在去除空格的文本中查找
    if not machine_found:
        for pattern in machine_patterns:
            pattern_no_space = pattern.replace(r'\s*', '')
            match = re.search(pattern_no_space, text_no_spaces)
            if match:
                result['串号'] = match.group(1)
                machine_found = True
                break
    
    if not machine_found:
        # 如果没有匹配到，查找所有4-6位数字
        all_numbers = re.findall(r'\b\d{4,6}\b', text)
        if all_numbers:
            # 优先选择看起来像编号的数字（排除年份等）
            for num in all_numbers:
                if not (num.startswith('202') or num.startswith('201')):  # 排除年份
                    result['串号'] = num
                    machine_found = True
                    break
            # 如果没找到合适的，用第一个
            if not machine_found and all_numbers:
                result['串号'] = all_numbers[0]
    
    if not machine_found:
        result['串号'] = ""

    # 探伤日期 - 去除空格影响
    date_patterns = [
        r'探伤日期\s*[:：\s]*(\d{4}-\d{2}-\d{2})',
        r'探伤时间\s*[:：\s]*(\d{4}-\d{2}-\d{2})',
        r'日期\s*[:：\s]*(\d{4}-\d{2}-\d{2})'
    ]
    
    date_found = False
    for pattern in date_patterns:
        match = re.search(pattern, text)
        if match:
            result['探伤日期'] = match.group(1)
            date_found = True
            break
    
    # 如果在原始文本中没找到，尝试在去除空格的文本中查找
    if not date_found:
        for pattern in date_patterns:
            pattern_no_space = pattern.replace(r'\s*', '')
            match = re.search(pattern_no_space, text_no_spaces)
            if match:
                result['探伤日期'] = match.group(1)
                date_found = True
                break
    
    if not date_found:
        # 如果没有匹配到特定格式，查找日期格式
        date_match = re.search(r'(\d{4}-\d{2}-\d{2})', text)
        if date_match:
            result['探伤日期'] = date_match.group(1)
    
    if not date_found:
        result['探伤日期'] = ""

    # 机型 - 模糊匹配，去除空格影响
    # 匹配各种可能的机型格式
    model_patterns = [
        r'机型[:：]?\s*([A-Z]{2,}-[A-Z0-9]+(/[A-Z0-9]+)?)',  # 标准格式：XX-XXX/XX
        r'([A-Z]{2,}-[A-Z0-9]+(/[A-Z0-9]+)?)',  # 直接匹配机型格式
    ]
    
    model_found = False
    for pattern in model_patterns:
        # 先在原始文本中匹配
        match = re.search(pattern, text, re.IGNORECASE)
        if match:
            result['机型'] = match.group(1)
            model_found = True
            break
        
        # 在去除空格的文本中匹配
        match = re.search(pattern, text_no_spaces, re.IGNORECASE)
        if match:
            result['机型'] = match.group(1)
            model_found = True
            break
    
    # 如果仍未找到机型，尝试更宽松的匹配
    if not model_found:
        # 匹配包含字母-数字/数字模式的任何字符串
        loose_match = re.search(r'([A-Z]{2,}-\d+[A-Z]*/?\d*[A-Z]*)', text, re.IGNORECASE)
        if loose_match:
            result['机型'] = loose_match.group(1)
    
    # 如果所有方法都失败，使用空字符串
    if not model_found:
        result['机型'] = ""
    
    print("=== 其他机型调试结束 ===")
    return result

def detect_model_type(text):
    """
    检测文本中的机型类型，去除空格影响
    """
    # 创建去除空格的文本版本
    text_no_spaces = re.sub(r'\s+', '', text)
    text_upper = text.upper()
    text_no_spaces_upper = text_no_spaces.upper()
    
    # 优先检测JGT-6M（去除空格）
    if "JGT-6M" in text_no_spaces_upper or "JGT6M" in text_no_spaces_upper:
        return "JGT-6M"
    # 检测GCT-8C/11（去除空格）
    elif "GCT-8C/11" in text_no_spaces_upper or "GCT8C/11" in text_no_spaces_upper:
        return "GCT-8C/11"
    # 检测是否有其他机型
    elif re.search(r'[A-Z]{2,}-[A-Z0-9]+(/[A-Z0-9]+)?', text_no_spaces_upper):
        return "OTHER"
    # 默认使用GCT-8C/11逻辑
    else:
        return "GCT-8C/11"

def extract_info_from_ocr(text):
    """
    从OCR文本中提取信息，根据机型自动选择处理逻辑
    """
    # 检测机型
    model_type = detect_model_type(text)
    print(f"检测到机型: {model_type}")
    
    if model_type == "JGT-6M":
        return extract_jgt6m_info(text)
    elif model_type == "OTHER":
        return extract_other_model_info(text)
    else:  # GCT-8C/11 或默认
        return extract_gct8c_info(text)

def save_ocr_text_to_file(text, output_path):
    """
    将OCR识别出的文本保存为txt文件
    """
    try:
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write(text)
        print(f"OCR识别文本已保存到: {output_path}")
        return True
    except Exception as e:
        print(f"保存OCR文本到文件时出错: {e}")
        return False

def create_ocr_debug_image(image_path, output_dir):
    """
    创建带OCR识别框的调试图像
    """
    try:
        img = cv2.imread(image_path)
        if img is None:
            print(f"无法读取图像: {image_path}")
            return False
            
        # 设置 OCR 语言（中英）
        custom_oem_psm_config = r'--oem 3 --psm 6 -l chi_sim+eng'
        
        # OCR 识别
        data = pytesseract.image_to_data(img, output_type=Output.DICT, config=custom_oem_psm_config)
        
        # 在图上绘制识别框与文字
        for i in range(len(data['text'])):
            text = data['text'][i].strip()
            if text == "":
                continue
            (x, y, w, h) = (data['left'][i], data['top'][i], data['width'][i], data['height'][i])
            cv2.rectangle(img, (x, y), (x + w, y + h), (0, 255, 0), 1)
            cv2.putText(img, text, (x, y - 5), cv2.FONT_HERSHEY_SIMPLEX, 0.4, (0, 255, 255), 1)
        
        # 保存带文字标注的图像
        base_name = os.path.splitext(os.path.basename(image_path))[0]
        out_path = os.path.join(output_dir, f"{base_name}_debug.png")
        cv2.imwrite(out_path, img)
        print(f"✅ 识别标注图已保存: {out_path}")
        return True
    except Exception as e:
        print(f"创建调试图像时出错: {e}")
        return False

def main(image_path, output_dir=None):
    """
    主函数：识别图片并提取信息
    """
    try:
        # 检查图片文件是否存在
        if not os.path.exists(image_path):
            print(f"错误：找不到图片文件 '{image_path}'")
            return None
        
        print(f"正在处理图片: {image_path}")
        
        # 打开图片
        image = Image.open(image_path)
        
        # 使用中英文混合OCR识别
        custom_config = r'--oem 3 --psm 6'
        ocr_text = pytesseract.image_to_string(image, lang='chi_sim+eng', config=custom_config)
        
        print("OCR识别结果：")
        print("=" * 50)
        print(ocr_text)
        print("=" * 50)
        
        # 确定输出目录
        if output_dir is None:
            output_dir = os.path.dirname(image_path)
        
        # 保存OCR文本到txt文件
        base_name = os.path.splitext(os.path.basename(image_path))[0]
        txt_output_path = os.path.join(output_dir, f"{base_name}_ocr_text.txt")
        save_ocr_text_to_file(ocr_text, txt_output_path)
        
        # 创建调试图像
        create_ocr_debug_image(image_path, output_dir)
        
        # 提取关键信息
        extracted_data = extract_info_from_ocr(ocr_text)
        
        # 确保所有必需的字段都存在
        required_fields = ["回放员", "串号", "探伤日期", "机型"]
        for field in required_fields:
            if field not in extracted_data:
                extracted_data[field] = ""
        
        # 保存到out.json
        output_path = os.path.join(output_dir, "out.json")
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(extracted_data, f, ensure_ascii=False, indent=2)
        
        print(f"\n提取的信息已保存到: {output_path}")
        print("最终提取的信息：")
        print(json.dumps(extracted_data, ensure_ascii=False, indent=2))
        
        return extracted_data
        
    except Exception as e:
        print(f"处理过程中出现错误：{e}")
        return None

if __name__ == "__main__":
    # 处理命令行参数
    if len(sys.argv) < 2:
        print("用法: python 8ctest.py <图片路径> [输出目录]")
        print("示例: python 8ctest.py C:/path/to/image.png")
        print("示例: python 8ctest.py C:/path/to/image.png C:/output/dir")
        sys.exit(1)
    
    image_path = sys.argv[1]
    output_dir = sys.argv[2] if len(sys.argv) > 2 else None
    
    result = main(image_path, output_dir)
    
    if result is None:
        sys.exit(1)
    else:
        sys.exit(0)